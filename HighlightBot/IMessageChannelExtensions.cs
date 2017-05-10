using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using Discord.Commands;

namespace HighlightBot
{
	public static class IMessageChannelExtensions
	{
		public static async Task<IUserMessage> SendEmbedMention(this IMessageChannel channel, IMessage message)
		{
			EmbedBuilder builder = new EmbedBuilder()
				.WithCurrentTimestamp()
				.WithDescription(message.Content);

			// Build author
			builder.WithAuthor(new EmbedAuthorBuilder()
				.WithName(message.Author.Username)
				.WithIconUrl(message.Author.GetAvatarUrl()));

			// Pick color
			builder.WithColor((message.Author as SocketGuildUser)?.Roles.OrderByDescending(r => r.Position)
				.FirstOrDefault()?.Color ?? Color.Default);

			// Build footer
			//builder.WithFooter(new EmbedFooterBuilder()
			//	.WithText($"Channel: {(message.Channel as SocketTextChannel).Mention}"));
			builder.AddInlineField("Channel", (message.Channel as SocketTextChannel).Mention);

			return await channel.SendMessageAsync("", false, builder);
		}

		public static async Task<IMessage> SendEmbed(this IMessageChannel channel, string message, Color color)
		{
			return await channel.SendMessageAsync("", false, new EmbedBuilder()
				.WithDescription(message)
				.WithColor(color));
		}

		public static async Task<IMessage> SendEmbedInfo(this IMessageChannel channel, string message)
			=> await SendEmbed(channel, message, new Color(255, 255, 0));

		public static async Task<IMessage> SendEmbedError(this IMessageChannel channel, string message)
			=> await SendEmbed(channel, message, new Color(255, 0, 0));

		public static async Task<IMessage> SendEmbedSuccess(this IMessageChannel channel, string message)
			=> await SendEmbed(channel, message, new Color(0, 128, 0));
	}
}
