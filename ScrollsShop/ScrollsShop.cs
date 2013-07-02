using System;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Threading;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;

namespace ScrollsShop
{
	public class ScrollsShop : BaseMod, ICommListener
	{
		public static bool loaded = false;

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
			return 1;
		}

		//only return MethodDefinitions you obtained through the scrollsTypes object
		//safety first! surround with try/catch and return an empty array in case it fails
		public static MethodDefinition[] GetHooks (TypeDefinitionCollection scrollsTypes, int version)
		{
			try
			{
				return new MethodDefinition[] {
					scrollsTypes["TradeSystem"].Methods.GetMethod("CloseTrade")[0]
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
			return false;
		}

		public override void AfterInvoke (InvocationInfo info, ref object returnValue)
		{
			if (info.target is TradeSystem && info.targetMethod.Equals("CloseTrade"))
			{
				Communicator com = App.Communicator;
				com.sendRequest(new LibraryViewMessage());
			}
		}

		public void handleMessage(Message msg) {
			if (msg is LibraryViewMessage) {
				LibraryViewMessage lvm = (LibraryViewMessage)msg;
				string TypeIdsString = "";
				bool ini = false;
				foreach (Card card in lvm.cards) {
					if (!ini) {
						ini = true;
					} else {
						TypeIdsString += ",";
					}
					TypeIdsString += card.typeId;
				}
				WebClient client = new WebClient ();
				//client.UploadStringAsync (new Uri ("http://localhost/scrollsshop.com/setCollection.php?name=" + App.MyProfile.ProfileInfo.name), "POST", TypeIdsString);
				client.UploadStringAsync (new Uri ("http://scrollsshop.com/setCollection.php?name=" + App.MyProfile.ProfileInfo.name), "POST", TypeIdsString);
			}
		}

		public void onReconnect() {

		}

	}
}