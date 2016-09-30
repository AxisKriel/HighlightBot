using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace HighlightBot.Extensions
{
	public static class DiscordClientExtensions
	{
		public static DiscordClient InstallCommands(this DiscordClient client)
		{
			client.GetService<CommandService>().CreateGroup("hl", x =>
			{
				x.CreateCommand("add")
					.Alias("add-word")
					.Description("Adds a word to your list of highlighted words.")
					.Parameter("word", ParameterType.Unparsed)
					.Do(async e =>
					{
						if (String.IsNullOrWhiteSpace(e.GetArg("word")))
						{
							await e.Channel.SendMessage("Syntax: `!hl add <word>`");
							return;
						}

						string word = e.GetArg("word").ToLowerInvariant();

						Db.User user;
						if (await Program.Db.Exists(e.User.Id))
						{
							// Fetch the user
							user = await Program.Db.GetUser(e.User.Id);
							if (user == null)
							{
								await e.Channel.SendMessage("An error occurred, please contact an administrator.");
								return;
							}
						}
						else
						{
							// Create the user
							user = Db.User.New(e.User.Id);
							if (!await Program.Db.AddUser(user))
							{
								await e.Channel.SendMessage("An error occurred, please contact an administrator.");
								return;
							}
						}

						if (!Program.Words.ContainsKey(word))
							Program.Words.Add(word, new List<Db.User>());

						if (Program.Words[word].Contains(user))
						{
							await e.Channel.SendMessage($"You are already highlighting `{e.GetArg("word")}`.");
							return;
						}
						else
						{
							if (await Program.Db.AddWord(user, word))
							{
								Program.Words[word].Add(user);
								await e.Channel.SendMessage($"Added `{e.GetArg("word")}` to your highlights."
									+ $"\nI'll PM you every time someone mentions `{e.GetArg("word")}` in their messages.");
							}
							else
								await e.Channel.SendMessage("An error occurred, please contact an administrator.");
						}
					});

				x.CreateCommand("remove")
					.Alias("del", "delete", "rm-word")
					.Description("Removes a word from your list of highlighted words.")
					.Parameter("word", ParameterType.Unparsed)
					.Do(async e =>
					{
						if (String.IsNullOrWhiteSpace(e.GetArg("word")))
						{
							await e.Channel.SendMessage("Syntax: `!hl remove <word>`");
							return;
						}

						string word = e.GetArg("word").ToLowerInvariant();

						Db.User user = await Program.Db.GetUser(e.User.Id);
						if (user == null)
						{
							await e.Channel.SendMessage("Your highlight list is empty.");
							return;
						}

						if (!Program.Words.ContainsKey(word) && !Program.Words[word].Contains(user))
						{
							await e.Channel.SendMessage($"You are not highlighting `{e.GetArg("word")}`.");
							return;
						}

						if (await Program.Db.RemoveWord(new Db.WordData(user.Id, word)))
						{
							Program.Words[word].Remove(user);
							await e.Channel.SendMessage($"Removed `{e.GetArg("word")}` from your highlights.");
						}
						else
							await e.Channel.SendMessage("An error occurred, please contact an administrator.");

					});

				x.CreateCommand("list")
					.Alias("show", "list-words")
					.Description("Lists all words in your list of highlighted words.")
					.Do(async e =>
					{
						if (!await Program.Db.Exists(e.User.Id))
						{
							await e.Channel.SendMessage("Your highlight list is empty."
								+ "\nUse `!hl add <word>` to be notified when someone uses said word in their messages.");
							return;
						}

						Db.User user = await Program.Db.GetUser(e.User.Id);
						if (user == null)
						{
							await e.Channel.SendMessage("An error occurred, please contact an administrator.");
							return;
						}

						IEnumerable<string> words = (await Program.Db.FindWords(user)).Select(w => $"`{w.Word}`");

						if (words.Count() == 0)
							await e.Channel.SendMessage("Your highlight list is empty.");
						else
							await e.Channel.SendMessage($"Current highlights: {String.Join(", ", words)}."
								+ "\nUse `!hl remove <word>` to remove one of your highlights.");
					});

				x.CreateCommand("enable")
				.Alias("disable")
				.Description("Enables or disables notifications for your highlights.")
				.Do(async e =>
				{
					if (!await Program.Db.Exists(e.User.Id))
					{
						await e.Channel.SendMessage("Your highlight list is empty."
								+ "\nUse `!hl add <word>` to be notified when someone uses said word in their messages.");
						return;
					}

					Db.User user = await Program.Db.GetUser(e.User.Id);
					if (user == null)
					{
						await e.Channel.SendMessage("An error occurred, please contact an administrator.");
						return;
					}

					bool enable = e.Message.Text.Contains("enable");
					if (await Program.Db.Enable(user, enable))
					{
						// Update cached data
						foreach (var list in Program.Words.Values)
						foreach (Db.User u in list.Where(u => u.ClientId == user.ClientId))
							u.Enabled = enable;

						if (enable)
							await e.Channel.SendMessage("You will now be notified by PM every time someone mentions one of your highlights.");
						else
							await e.Channel.SendMessage("You will no longer receive notifications from me.");
					}
					else
						await e.Channel.SendMessage("An error occurred, please contact an administrator.");
				});
		});

			return client;
		}
	}
}
