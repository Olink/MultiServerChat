using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using HttpServer;
using Newtonsoft.Json;
using Rests;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MultiServerChat
{
	[ApiVersion(1,14)]
	public class MultiServerChat : TerrariaPlugin
	{
		ConfigFile Config = new ConfigFile();
		private string savePath = "";

		public override string Author
		{
			get { return "Zack Piispanen"; }
		}

		public override string Description
		{
			get { return "Facilitate chat between servers."; }
		}

		public override string Name
		{
			get { return "Multiserver Chat"; }
		}

		public override Version Version
		{
			get{ return new Version(1, 0, 0, 0); }
		}

		public MultiServerChat(Main game) : base(game)
		{
			savePath = Path.Combine(TShock.SavePath, "multiserverchat.json");
			Config = ConfigFile.Read(savePath);
			Config.Write(savePath);
			TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
		}

		public override void Initialize()
		{
			ServerApi.Hooks.ServerChat.Register(this, OnChat, 10);
			TShock.RestApi.Register(new SecureRestCommand("/msc", RestChat, "msc.canchat"));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
			}
			base.Dispose(disposing);
		}

		private void OnReload(ReloadEventArgs args)
		{
			if (args.Player.Group.HasPermission("msc.reload"))
			{
				Config = ConfigFile.Read(savePath);
				Config.Write(savePath);
			}
		}

		private object RestChat(RestVerbs verbs, IParameterCollection parameters, SecureRest.TokenData tokenData)
		{
			if (!Config.DisplayChat)
				return new RestObject();

			if (!string.IsNullOrWhiteSpace(parameters["message"]))
			{
				var bytes = Convert.FromBase64String(parameters["message"]);
				var str = Encoding.UTF8.GetString(bytes);
				var message = Message.FromJson(str);
				TShock.Utils.Broadcast(message.Text, message.Red, message.Green, message.Blue);
			}

			return new RestObject();
		}

		private bool failure = false;
		private void OnChat(ServerChatEventArgs args)
		{
			if (!Config.SendChat)
				return;

			var tsplr = TShock.Players[args.Who];
			if (tsplr == null)
			{
				return;
			}

			if (args.Text.StartsWith("/") && args.Text.Length > 1)
			{
			}
			else
			{
				if (!tsplr.Group.HasPermission(Permissions.canchat))
				{
					return;
				}

				if (tsplr.mute)
				{
					return;
				}

				var message = new Message()
				{
					Text =
						String.Format(Config.ChatFormat, TShock.Config.ServerName, tsplr.Group.Name, tsplr.Group.Prefix, tsplr.Name, tsplr.Group.Suffix,
							args.Text),
					Red = tsplr.Group.R,
					Green = tsplr.Group.G,
					Blue = tsplr.Group.B
				};

				var bytes = Encoding.UTF8.GetBytes(message.ToString());
				var base64 = Convert.ToBase64String(bytes);

				var uri = String.Format("{0}?message={1}&token={2}", Config.RestURL, base64, Config.Token);

				try
				{
					var request = (HttpWebRequest)WebRequest.Create(uri);
					using (var res = request.GetResponse())
					{}
					failure = false;
				}
				catch (Exception)
				{
					if (!failure)
					{
						Log.Error("Failed to make request to other server, server is down?");
						failure = true;
					}
				}
			}
		}
	}

	public class Message
	{
		public string Text { get; set; }
		public byte Red { get; set; }
		public byte Green { get; set; }
		public byte Blue { get; set; }

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		public static Message FromJson(string js)
		{
			return JsonConvert.DeserializeObject<Message>(js);
		}
	}
}
