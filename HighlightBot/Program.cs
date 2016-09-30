using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using HighlightBot.Extensions;
using System.IO;

namespace HighlightBot
{
	class Program
	{
		private static string Token
		{
			get
			{
				if (!File.Exists("Highlights.token"))
					return null;
				else
					return File.ReadAllText("Highlights.token");
			}
		}

		public static DiscordClient Bot { get; private set; }

		public static Db.Manager Db { get; private set; }

		public static Server Server => Bot?.Servers?.FirstOrDefault();

		public static Dictionary<string, List<Db.User>> Words { get; private set; }

		static void Main(string[] args)
		{
			Words = new Dictionary<string, List<Db.User>>();

			Bot = new DiscordClient(x =>
			{
				x.AppName = "Highlight Bot";
				x.AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

				// Once I figure this out lol
				x.AppUrl = "";
			});

			Bot.UsingCommands(x =>
			{
				x.AllowMentionPrefix = true;
				x.HelpMode = HelpMode.Public;
				x.PrefixChar = '!';
			}).InstallCommands();

			Bot.MessageReceived += async (o, e) =>
			{
				if (!e.Message.IsAuthor && !e.Channel.IsPrivate && !String.IsNullOrWhiteSpace(e.Message.Text))
				{
					foreach (string w in Words.Keys)
					{
						if (e.Message.Text.ToLowerInvariant().Contains(w))
						{
							// Encase the word in backticks to make it more visible
							string msg = e.Message.Text.Replace(w, $"`{w}`");

							// Notify each user
							foreach (Db.User u in Words[w].Where(u => u.Enabled))
							{
								User discordU = e.Server.GetUser((ulong)u.ClientId);

								// User cannot be null
								if (discordU == null
								// Avoid self-mentions
								 || discordU.Id == e.User.Id
								// Only notify users of channel messages they can see
								 || !discordU.Channels.Contains(e.Channel)
								// Avoid notifications when the user is already being mentioned
								 || e.Message.MentionedUsers.Contains(discordU))
									continue;

								await discordU.SendMessage($"{e.User.NicknameMention ?? e.User.Mention} mentioned you at {e.Channel.Mention}:"
									+ $"\n<{e.User.Nickname ?? e.User.Name}> {msg}");
							}
						}
					}
				}
			};

			// Set the Manager instance and create the tables
			Db = new Db.Manager();

			// Connect the bot and start listening
			Task.Run(Connect);

			// Command loop
			CommandLoop();
		}

		static async Task Connect()
		{
			if (String.IsNullOrWhiteSpace(Token))
				Console.WriteLine(" * Please set a bot token by typing 'set-token <token>'."
					+ "\n  * The bot will automatically connect once a token is set.");
			else
				await Bot.Connect(Token, TokenType.Bot);

			if (Bot.State == ConnectionState.Connected)
				Console.WriteLine(" * Highlights bot connected.");
		}

		static void CommandLoop()
		{
			while (true)
			{
				string cmd = Console.ReadLine().ToLowerInvariant();
				switch (cmd)
				{
					case "token":
					case "set-token":
						string[] args = cmd.Split(' ');
						if (args.Length > 1)
						{
							string token = args[1];
							File.WriteAllText("Highlights.token", token);

							Console.WriteLine($"  * Bot token set to '{token}'.");
							Task.Run(Connect);
						}
						break;
					case "users":
						Console.WriteLine($"Current users:");
						foreach (string k in Words.Keys)
							Console.WriteLine($"  {k}: {String.Join(", ", Words[k].Select(u => u.ClientId))}");
						break;
					case "exit":
					case "off":
					case "quit":
						if (Bot.State == ConnectionState.Connected)
							Bot.ExecuteAndWait(Bot.Disconnect);
						return;
					default:
						break;
				}
			}
		}
	}
}
