using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Mono.Data.Sqlite;
using System.IO;

namespace HighlightBot.Db
{
	public class Manager
	{	
		public static string ConnectionString => $"Data Source={Path.Combine(Environment.CurrentDirectory, FileName)};Version=3";
		public const string FileName = "Highlights.sqlite";

		internal Manager()
		{
			Task.Run(CreateTables);
		}

		private Task CreateTables()
		{
			if (!File.Exists(FileName))
			{
				return Task.Run(() =>
				{
					//SqliteConnection.CreateFile(FileName);

					using (var db = OpenConnection())
					{
						string sql = new StringBuilder("CREATE TABLE Users (")
							.Append("Id INTEGER PRIMARY KEY AUTOINCREMENT").Append(", ")
							.Append("ClientId BIGINT UNIQUE NOT NULL").Append(", ")
							.Append("Enabled INT NOT NULL").Append(')')
							.ToString();
						db.Execute(sql);

						sql = new StringBuilder("CREATE TABLE Words (")
							.Append("User INT NOT NULL").Append(", ")
							.Append("Word TEXT NOT NULL").Append(", ")
							.Append("FOREIGN KEY(User) REFERENCES Users(Id)").Append(' ')
							.Append("ON DELETE CASCADE ON UPDATE CASCADE").Append(')')
							.ToString();
						db.Execute(sql);
					}
				});
			}
			else
				return Task.Run(LoadData);
		}

		protected IDbConnection OpenConnection()
		{
			return new SqliteConnection(ConnectionString);
		}

		public async Task<bool> AddUser(User user)
		{
			using (var db = OpenConnection())
			{
				bool s = await db.ExecuteAsync("INSERT INTO Users (ClientId, Enabled) VALUES (@ClientId, @Enabled)", user) == 1;
				if (s)
				{
					// I can't trust last_insert_rowid() due to all the async work being done here, so I need to get the id manually
					user.Id = await db.QuerySingleOrDefaultAsync<int>("SELECT Id FROM Users WHERE ClientId = @ClientId", user);
					return true;
				}
				else
					return false;
			}
		}

		public async Task<bool> AddWord(User user, string word)
		{;
			using (var db = OpenConnection())
				return await db.ExecuteAsync("INSERT INTO Words (User, Word) VALUES (@User, @Word)", new WordData(user.Id, word.ToLowerInvariant())) == 1;
		}

		public async Task<bool> Enable(User user, bool enabled = true)
		{
			using (var db = OpenConnection())
				return await db.ExecuteAsync("UPDATE Users SET Enabled = @Enabled WHERE Id = @Id",
					new { Id = user.Id, Enabled = enabled }) == 1;
		}

		public async Task<bool> Exists(ulong clientId)
		{
			long id = (long)clientId;
			using (var db = OpenConnection())
				return await db.QuerySingleOrDefaultAsync<User>("SELECT * FROM Users WHERE ClientId = @ClientId", new { ClientId = id }) != null;
		}

		public async Task<IEnumerable<WordData>> FindWords(User user)
		{
			using (var db = OpenConnection())
				return await db.QueryAsync<WordData>("SELECT * FROM Words WHERE User = @Id", user);
		}

		public async Task<User> GetUser(ulong clientId)
		{
			long id = (long)clientId;
			using (var db = OpenConnection())
				return await db.QuerySingleOrDefaultAsync<User>("SELECT * FROM Users WHERE ClientId = @ClientId", new { ClientId = id });
		}

		public async Task LoadData()
		{
			string wordSql = "SELECT * FROM Words";
			string userSql = "SELECT * FROM Users WHERE Id = @User";
			using (var db = OpenConnection())
			{
				IEnumerable<WordData> words = await db.QueryAsync<WordData>(wordSql);

				foreach (WordData wd in words)
				{
					User u = await db.QuerySingleOrDefaultAsync<User>(userSql, wd);
					if (u != null)
					{
						if (!Program.Words.ContainsKey(wd.Word))
							Program.Words[wd.Word] = new List<User> { u };
						else
							Program.Words[wd.Word].Add(u);
					}
				}
			}
		}

		public async Task<bool> RemoveWord(WordData word)
		{
			using (var db = OpenConnection())
				return await db.ExecuteAsync("DELETE FROM Words WHERE User = @User AND Word = @Word", word) == 1;
		}
	}
}
