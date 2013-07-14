using System;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Threading;
using JsonFx.Json;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;

namespace ScrollsShop
{
	public class ScrollsShop : BaseMod, ICommListener
	{
		public static bool loaded = false;
		public static bool inTrade = false;
		public List<Card> offeredCardsP1 = new List<Card>();
		public List<Card> offeredCardsP2 = new List<Card>();
		public FieldInfo cardsField = null;
		public CardListPopup cardListPopupP1 = null;
		public CardListPopup cardListPopupP2 = null;

		//initialize everything here, Game is loaded at this point
		public ScrollsShop ()
		{
			App.Communicator.addListener(this);
		}


		public static string GetName ()
		{
			return "ScS-Utilities";
		}

		public static int GetVersion ()
		{
			return 4;
		}

		//only return MethodDefinitions you obtained through the scrollsTypes object
		//safety first! surround with try/catch and return an empty array in case it fails
		public static MethodDefinition[] GetHooks (TypeDefinitionCollection scrollsTypes, int version)
		{
			try
			{
				return new MethodDefinition[] {
					scrollsTypes["MainMenu"].Methods.GetMethod("Start")[0],
					scrollsTypes["TradeSystem"].Methods.GetMethod("CloseTrade")[0],
					scrollsTypes["Communicator"].Methods.GetMethod("sendRequest", new Type[]{typeof(Message)}),
					scrollsTypes["TradeSystem"].Methods.GetMethod("StartTrade")[0]
				};
			}
			catch
			{
				return new MethodDefinition[] { };
			}
		}


		public override bool BeforeInvoke (InvocationInfo info, out object returnValue)
		{
			returnValue = null;
			if (info.targetMethod.Equals("sendRequest"))
			{
				if (info.arguments[0] is RoomChatMessageMessage)
				{
					RoomChatMessageMessage rcmm = (RoomChatMessageMessage)info.arguments[0];

					if (rcmm.text.Equals("/total"))
					{
						if (updateTradeOffer())
						{
							string typeIdsString = cardListToString(offeredCardsP1);
							//App.Popups.ShowOk (null, "debug", "Debug", "Going to check: "+typeIdsString, "Dismiss");
							WebClient client = new WebClient ();
							client.UploadStringAsync (new Uri ("http://scrollsshop.com/getTotal.php?name=" + App.MyProfile.ProfileInfo.name), "POST", typeIdsString);
							client.UploadStringCompleted += (sender, e) =>
							{

								JsonReader reader = new JsonReader();
								var template = new { text=String.Empty, msg=String.Empty };
								var msg = reader.Read(e.Result,template);
								if (msg.msg != "success")
								{
									displayMessage("Error: "+msg.text);
								} else {
									displayMessage("Total: "+msg.text);
								}
							};

						} else {
							displayMessage ("You are not in a trade.");
						}
						return true;
					}
					if (rcmm.text.Equals("/wts"))
					{
						WebClient client = new WebClient ();
						client.UploadStringAsync (new Uri ("http://scrollsshop.com/getWTS.php?name=" + App.MyProfile.ProfileInfo.name), "POST");
						client.UploadStringCompleted += (sender, e) =>
						{

							JsonReader reader = new JsonReader();
							var template = new { text=String.Empty, msg=String.Empty };
							var msg = reader.Read(e.Result,template);
							if (msg.msg != "success")
							{
								displayMessage("Error: "+msg.text);
							} else {
								sendMessage(msg.text);
							}
						};

						return true;
					}

				}
				return false;
			}
			return false;
		}

		public override void AfterInvoke (InvocationInfo info, ref object returnValue)
		{
			if (info.target is MainMenu && info.targetMethod.Equals("Start"))
			{
				App.ArenaChat.RoomEnter("ScrollsShop");
			}
			if (info.target is TradeSystem && info.targetMethod.Equals("CloseTrade"))
			{
				App.Communicator.sendRequest(new LibraryViewMessage());
				inTrade = false;
				//sendMessage("This is your offer: "+offeredCards.ToString());
			}
			if (info.target is TradeSystem && info.targetMethod.Equals("StartTrade"))
			{
				inTrade = true;

				FieldInfo cardListField = typeof(TradeSystem).GetField ("clOfferP1", BindingFlags.NonPublic | BindingFlags.Instance);
				cardListPopupP1 = (CardListPopup) cardListField.GetValue((TradeSystem)info.target);
				cardListField = typeof(TradeSystem).GetField ("clOfferP2", BindingFlags.NonPublic | BindingFlags.Instance);
				cardListPopupP2 = (CardListPopup) cardListField.GetValue((TradeSystem)info.target);

				cardsField = typeof(CardListPopup).GetField ("cards", BindingFlags.NonPublic | BindingFlags.Instance);
				updateTradeOffer ();
			}
		}

		public bool updateTradeOffer() {
			if (!inTrade || cardsField == null || cardListPopupP1 == null || cardListPopupP2 == null) {
				//offeredCardsP1 = new List<Card> ();
				//offeredCardsP2 = new List<Card> ();
				return false;
			}
			offeredCardsP1 = (List<Card>) cardsField.GetValue (cardListPopupP1);
			offeredCardsP2 = (List<Card>) cardsField.GetValue (cardListPopupP2);
			return true;
		}

		protected void displayMessage(string message)
		{
			RoomChatMessageMessage msg = new RoomChatMessageMessage();
			msg.from = GetName();
			msg.text = "<color=#aa803f>"+message+"</color>";
			msg.roomName = App.ArenaChat.ChatRooms.GetCurrentRoom();

			App.ChatUI.handleMessage(msg);
			App.ArenaChat.ChatRooms.ChatMessage(msg);
		}

		protected void sendMessage(string message)
		{
			RoomChatMessageMessage msg = new RoomChatMessageMessage();
			msg.from = GetName();
			msg.text = message;
			msg.roomName = App.ArenaChat.ChatRooms.GetCurrentRoom();
			App.Communicator.sendRequest (msg);
		}

		public void handleMessage(Message msg) {
			if (msg is LibraryViewMessage) {
				LibraryViewMessage lvm = (LibraryViewMessage)msg;
				if (App.MyProfile.ProfileInfo.id != lvm.profileId)
					return;
				string typeIdsString = cardListToString(lvm.cards);
				WebClient client = new WebClient ();
				//client.UploadStringAsync (new Uri ("http://localhost/scrollsshop.com/setCollection.php?name=" + App.MyProfile.ProfileInfo.name), "POST", TypeIdsString);
				client.UploadStringAsync (new Uri ("http://scrollsshop.com/setCollection.php?name=" + App.MyProfile.ProfileInfo.name), "POST", typeIdsString);
				client.UploadStringCompleted += (sender, e) =>
				{
					JsonReader reader = new JsonReader();
					var template = new { text=String.Empty, msg=String.Empty };
					var msg2 = reader.Read(e.Result,template);
					if (msg2.msg != "success")
					{
						displayMessage("Error: "+msg2.text);
					}
				};
			}
		}

		public string cardListToString(List<Card> cards) {
			return cardListToString (cards.ToArray ());
		}

		public string cardListToString(Card[] cards) {
			string typeIdsString = "";
			bool ini = false;
			foreach (Card card in cards) {
				if (!ini) {
					ini = true;
				} else {
					typeIdsString += ",";
				}
				typeIdsString += card.typeId;
				typeIdsString += cardLevelToChar (card.level);
			}
			return typeIdsString;
		}

		public string cardLevelToChar(int level) {
			switch(level) {
			case 0:
				return "a";
			case 1:
				return "b";
			case 2:
				return "c";
			case 3:
				return "d";
			}
			return "";
		}

		public void onReconnect() {

		}

	}
}