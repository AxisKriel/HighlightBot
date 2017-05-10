using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;

namespace HighlightBot.Modules
{
	[Group("hl")]
	public class HighlightModule : ModuleBase
	{
		

		[Alias("add-word"), Command("add"), Summary("Adds a word to your list of highlighted words.")]
		async Task Add([Remainder, Summary("The word to highlight.")] string word)
		{
			if (String.IsNullOrWhiteSpace(word))
			{
				await Context.Channel.SendEmbedInfo("Syntax: `!hl add <word>`");
				return;
			}

			word = word.ToLowerInvariant();

			Db.User user;
			if (await Program.Db.Exists(Context.User.Id))
			{
				// Fetch the user
				user = await Program.Db.GetUser(Context.User.Id);
				if (user == null)
				{
					await Context.Channel.SendEmbedError("An error occurred, please contact an administrator.");
					return;
				}
			}
			else
			{
				// Create the user
				user = Db.User.New(Context.User.Id);
				if (!await Program.Db.AddUser(user))
				{
					await Context.Channel.SendEmbedError("An error occurred, please contact an administrator.");
					return;
				}
			}

			if (!Program.Words.ContainsKey(word))
				Program.Words.Add(word, new List<Db.User>());

			if (Program.Words[word].Contains(user))
			{
				await Context.Channel.SendEmbedInfo($"You are already highlighting `{word}`.");
				return;
			}
			else
			{
				if (await Program.Db.AddWord(user, word))
				{
					Program.Words[word].Add(user);
					await Context.Channel.SendEmbedSuccess($"Added `{word}` to your highlights."
						+ $"\nI'll PM you every time someone mentions `{word}` in their messages.");
				}
				else
					await Context.Channel.SendEmbedSuccess("An error occurred, please contact an administrator.");
			}
		}

		[Alias("del", "delete", "rm-word"), Command("remove"), Summary("Removes a word from your list of highlighted words.")]
		async Task Remove([Remainder, Summary("The word to stop highlighting.")] string word)
		{
			if (String.IsNullOrWhiteSpace(word))
			{
				await Context.Channel.SendEmbedInfo("Syntax: `!hl remove <word>`");
				return;
			}

			word = word.ToLowerInvariant();

			Db.User user = await Program.Db.GetUser(Context.User.Id);
			if (user == null)
			{
				await Context.Channel.SendEmbedInfo("Your highlight list is empty.");
				return;
			}

			if (!Program.Words.ContainsKey(word) && !Program.Words[word].Contains(user))
			{
				await Context.Channel.SendEmbedError($"You are not highlighting `{word}`.");
				return;
			}

			if (await Program.Db.RemoveWord(new Db.WordData(user.Id, word)))
			{
				Program.Words[word].Remove(user);
				await Context.Channel.SendEmbedSuccess($"Removed `{word}` from your highlights.");
			}
			else
				await Context.Channel.SendEmbedError("An error occurred, please contact an administrator.");
		}

		[Alias("show", "list-words"), Command("list"), Summary("Lists all words in your list of highlighted words.")]
		async Task List()
		{
			if (!await Program.Db.Exists(Context.User.Id))
			{
				await Context.Channel.SendEmbedInfo("Your highlight list is empty."
					+ "\nUse `!hl add <word>` to be notified when someone uses said word in their messages.");
				return;
			}

			Db.User user = await Program.Db.GetUser(Context.User.Id);
			if (user == null)
			{
				await Context.Channel.SendEmbedError("An error occurred, please contact an administrator.");
				return;
			}

			IEnumerable<string> words = (await Program.Db.FindWords(user)).Select(w => $"`{w.Word}`");

			if (words.Count() == 0)
				await Context.Channel.SendEmbedSuccess("Your highlight list is empty.");
			else
				await Context.Channel.SendEmbedSuccess($"Current highlights: {String.Join(", ", words)}."
					+ "\nUse `!hl remove <word>` to remove one of your highlights.");
		}

		[Alias("disable"), Command("enable"), Summary("Enables or disables notifications for your highlights.")]
		async Task Enable()
		{
			if (!await Program.Db.Exists(Context.User.Id))
			{
				await Context.Channel.SendEmbedInfo("Your highlight list is empty."
						+ "\nUse `!hl add <word>` to be notified when someone uses said word in their messages.");
				return;
			}

			Db.User user = await Program.Db.GetUser(Context.User.Id);
			if (user == null)
			{
				await Context.Channel.SendEmbedError("An error occurred, please contact an administrator.");
				return;
			}

			bool enable = Context.Message.Content.Contains("enable");
			if (await Program.Db.Enable(user, enable))
			{
				// Update cached data
				foreach (var list in Program.Words.Values)
					foreach (Db.User u in list.Where(u => u.ClientId == user.ClientId))
						u.Enabled = enable;

				if (enable)
					await Context.Channel.SendEmbedSuccess("You will now be notified by PM every time someone mentions one of your highlights.");
				else
					await Context.Channel.SendEmbedSuccess("You will no longer receive notifications from me.");
			}
			else
				await Context.Channel.SendEmbedError("An error occurred, please contact an administrator.");
		}
	}
}
