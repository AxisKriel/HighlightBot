using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using System.IO;
using Discord.WebSocket;
using System.Reflection;

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

		public static DiscordSocketClient Bot { get; private set; }

		public static CommandService Commands { get; private set; }

		public static Db.Manager Db { get; private set; }

		public static Dictionary<string, List<Db.User>> Words { get; private set; }

		static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

		public async Task Start()
		{
			Words = new Dictionary<string, List<Db.User>>();

			Bot = new DiscordSocketClient();
			Bot.MessageReceived += HandleMentions;
			Bot.Log += (msg) =>
			{
				Console.WriteLine(msg.ToString());
				return Task.CompletedTask;
			};

			Commands = new CommandService();
			await InstallCommands();

			// Set the Manager instance and create the tables
			Db = new Db.Manager();

			// Connect the bot and start listening
			await Connect();

			// Command loop
			await CommandLoop();
		}

		public async Task Connect()
		{
			if (String.IsNullOrWhiteSpace(Token))
				Console.WriteLine(" * Please set a bot token by typing 'set-token <token>'."
					+ "\n * The bot will automatically connect once a token is set.");
			else
			{
				try
				{
					await Bot.LoginAsync(TokenType.Bot, Token);
				}
				catch (HttpException ex)
				{
					Console.WriteLine(ex.Message);
				}
				if (Bot.LoginState == LoginState.LoggedOut)
					Console.WriteLine(" * An invalid token was provided, so the connection process has been aborted.");
				else
					await Bot.StartAsync();
			}
		}

		private async Task CommandLoop()
		{
			while (true)
			{
				string cmd = Console.ReadLine();
				string[] args = cmd.Split(' ');
				switch (args[0].ToLowerInvariant())
				{
					case "token":
					case "set-token":
						if (args.Length > 1)
						{
							string token = args[1];
							File.WriteAllText("Highlights.token", token);

							Console.WriteLine($" * Bot token set to '{token}'.");
							await Connect();
						}
						break;
					case "status":
						Console.WriteLine($"Status: {Bot.ConnectionState} ({Bot.LoginState})");
						break;
					case "users":
						Console.WriteLine($"Current users:");
						foreach (string k in Words.Keys)
							Console.WriteLine($"  {k}: {String.Join(", ", Words[k].Select(u => u.ClientId))}");
						break;
					case "exit":
					case "off":
					case "quit":
						if (Bot.ConnectionState == ConnectionState.Connected)
						{
							await Bot.LogoutAsync();
							await Bot.StopAsync();

							// Give the bot time to disconnect properly before ending the process
							await Task.Delay(500);
						}
						return;
					default:
						break;
				}
			}
		}

		public async Task InstallCommands()
		{
			// Hook the MessageReceived Event into our Command Handler
			Bot.MessageReceived += HandleCommand;

			// Discover all of the commands in this assembly and load them.
			await Commands.AddModulesAsync(Assembly.GetEntryAssembly());
		}

		public async Task HandleCommand(SocketMessage messageParam)
		{
			// Don't process the command if it was a System Message
			var message = messageParam as SocketUserMessage;
			if (message == null)
				return;

			// Create a number to track where the prefix ends and the command begins
			int argPos = 0;

			// Determine if the message is a command, based on if it starts with '!' or a mention prefix
			if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(Bot.CurrentUser, ref argPos)))
				return;

			// Create a Command Context
			var context = new CommandContext(Bot, message);

			// Execute the command. (result does not indicate a return value, 
			// rather an object stating if the command executed succesfully)
			var result = await Commands.ExecuteAsync(context, argPos);
			// Ignore unknown command error due to collision with other bots
			if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
				await context.Channel.SendEmbedError(result.ErrorReason);
		}

		public async Task HandleMentions(SocketMessage messageParam)
		{
			var message = messageParam as SocketUserMessage;
			if (message == null)
				return;

			if (message.Author.Id != Bot.CurrentUser.Id && !Bot.DMChannels.Contains(message.Channel) && !String.IsNullOrWhiteSpace(message.Content))
			{
				foreach (string w in Words.Keys)
				{
					if (message.Content.ToLowerInvariant().Contains(w))
					{
						// Encase the word in backticks to make it more visible
						string msg = message.Content.Replace(w, $"`{w}`");

						// Notify each user
						foreach (Db.User u in Words[w].Where(u => u.Enabled))
						{
							IUser socketUser = await message.Channel.GetUserAsync((ulong)u.ClientId);

							// User cannot be null
							if (socketUser == null
							 // Avoid self-mentions
							 || socketUser.Id == message.Author.Id
							 // Only notify users of channel messages they can see
							 || (socketUser as SocketGuildUser)?.GetPermissions(message.Channel as SocketGuildChannel).ReadMessages != true
							 // Avoid notifications when the user is already being mentioned
							 || message.MentionedUsers.Contains(socketUser))
								continue;

							await (await socketUser.CreateDMChannelAsync()).SendEmbedMention(message);
							//await (await socketUser.CreateDMChannelAsync()).SendMessageAsync(
							//	$"{(message.Author as SocketGuildUser).Nickname ?? message.Author.Mention} mentioned you at {(message.Channel as SocketTextChannel).Mention}:"
							//	+ $"\n<{(message.Author as SocketGuildUser).Nickname ?? message.Author.Mention}> {msg}");
						}
					}
				}
			}
		}
	}
}
