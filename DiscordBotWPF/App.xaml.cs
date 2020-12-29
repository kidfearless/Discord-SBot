using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System.Windows.Navigation;
using DSharpPlus.Entities;
using System.Reflection;
using System.Net;
using DiscordBotWPF.Models;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Win32;

namespace DiscordBotWPF
{
	public partial class App : Application, IDisposable
	{
		const string SBOX_URL = "https://commits.facepunch.com/r/sbox?format=json&p=";
		const string TOKEN_NAME = "DISCORD_SBOT_TOKEN";
		const string CHANNEL_NAME = "sbox-updates";
		const ulong BHOPTIMER_SBOX_UPDATES = 778315425901051924;
		const int RESULTS_PER_PAGE = 50;
		DiscordClient Client;
		DiscordChannel Channel;
		HashSet<Commit> Commits = new HashSet<Commit>();
		readonly string CommitsPath = InitPath();
		Timer CronTimer;

		private static string InitPath()
		{
			var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var folder = Path.Combine(basePath, "SBot");
			if (!Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
			}
			var path = Path.Combine(folder, "commits.json");
			if (!File.Exists(path))
			{
				File.Create(path).Dispose();
			}
			return path;
		}



		public App()
		{
		}

		~App()
		{
			this.Dispose();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			this.Dispose();

			TryUpdateCommitFile();


			base.OnExit(e);
		}

		// Entry Point for our application
		protected override async void OnStartup(StartupEventArgs e)
		{
			//AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

			AddToLaunchWithWindows();

			

			var cfg = new DiscordConfiguration
			{
				Token = Environment.GetEnvironmentVariable(TOKEN_NAME),
				TokenType = TokenType.Bot,

				AutoReconnect = true
			};
			Client = new DiscordClient(cfg);
			Client.GuildAvailable += Client_GuildAvailable;
			await Client.ConnectAsync();
			base.OnStartup(e);
		}

		private void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
		{
			try
			{
				//string dir = Path.GetDirectoryName(path);
				//string exceptionPath = Path.Combine(dir, "Exceptions.log");
				//using var stream = File.Open(exceptionPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
				//using var writer = new StreamWriter(stream);
				//writer.WriteLine(e.Exception);
				//writer.WriteLine(e.Exception.StackTrace + "\n");
			}
			catch
			{

			}
		}

		// Called when we have connected to our discord and can start sending messages.
		// This will create our repeating timer at a safe point.
		private Task Client_GuildAvailable(GuildCreateEventArgs e)
		{
			Channel = e.Guild.Channels.First(chan => chan.Name.ToLower() == CHANNEL_NAME);

			GetAllCommits();

			// Initialize our timer
			CronTimer = new Timer(OnTick, null, 0, 15000);

			return Task.CompletedTask;
		}

		// called every 15 seconds to check for changes to the commit log.
		// I could probably make the delay bigger but i'd like it at least somewhat repsonsive.
		private void OnTick(object state)
		{
			if(GetJsonString(SBOX_URL + 1, out string json))
			{
				var commits = JsonConvert.DeserializeObject<CommitHolder>(json);
				var newCommits = commits.Results.Except(Commits, new CommitComparer()).ToArray();
				Array.Sort(newCommits, new CommitSorter());
				TryUpdateCommitFile();
				Commits.UnionWith(newCommits);
				foreach (var commit in newCommits)
				{
					SendCommit(commit);
				}
			}
		}

		private void GetAllCommits()
		{

			#region Retrieve previous sessions commits
			var stream = File.OpenRead(CommitsPath);
			if (stream.Length > 0)
			{
				using var reader = new StreamReader(stream);
				var commits = JsonConvert.DeserializeObject<HashSet<Commit>>(reader.ReadToEnd());
				if (commits is not null)
				{
					Commits.UnionWith(commits);
				}
			}
			stream.Dispose();
			#endregion

			#region Retrieve any commits we dont have
			List<Commit> tempCommits = new List<Commit>();
			int i = 1;
			while (GetJsonString(SBOX_URL + i, out string json))
			{
				// get's the commits that haven't been added yet and adds them
				var holder = JsonConvert.DeserializeObject<CommitHolder>(json);
				var newCommits = holder.Results.Except(Commits, new CommitComparer()).ToArray();
				tempCommits.AddRange(newCommits);
				// If we added 24 commits when there's 50 commits per page
				// Then that implies that we've already added the previous pages commits
				// Hopefully they don't add 50+ commits in one day
				if (newCommits.Length < RESULTS_PER_PAGE)
				{
					break;
				}

				i++;
			}

			// print the new commits in order
			tempCommits.Sort(new CommitSorter());
			foreach (var commit in tempCommits)
			{
				SendCommit(commit);
			}

			Commits.UnionWith(tempCommits);

			TryUpdateCommitFile();
			#endregion
		}

		// Helper method for reading the given url as a string
		bool GetJsonString(string url, out string json)
		{
			json = null;
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			using var response = (HttpWebResponse)request.GetResponse();

			// does this evere return anything else?
			if (response.StatusCode == HttpStatusCode.OK)
			{
				using var stream = response.GetResponseStream();
				using StreamReader readStream = new StreamReader(stream);

				json = readStream.ReadToEnd();
				return true;
			}

			return false;
		}

		void SendCommit(Commit commit)
		{
			var message = " - " + commit.Message.Replace("\n", "\n - ");
			var embed = new DiscordEmbedBuilder();
			embed.Color = DiscordColor.CornflowerBlue;

			embed.WithTitle($"{commit.User.Name} on {commit.Repo}/{commit.Branch}");
			embed.WithUrl($"https://commits.facepunch.com/{commit.ID}");

			embed.WithAuthor(commit.User.Name, icon_url: commit.User.Avatar);
			embed.WithDescription(message);

			embed.WithThumbnailUrl(commit.User.Avatar);

			DateTime.TryParse(commit.Created, out DateTime created);
			embed.WithTimestamp(created.ToLocalTime());

			Client.SendMessageAsync(Channel, embed: embed).GetAwaiter().GetResult();
		}


		private void TryUpdateCommitFile()
		{
			try
			{
				using var stream = File.Open(CommitsPath, FileMode.Open);
				stream.SetLength(0);
				using var writer = new StreamWriter(stream)
				{
					AutoFlush = true
				};
				writer.Write(JsonConvert.SerializeObject(Commits, Formatting.Indented));
			} catch{ }
		}

		public void Dispose()
		{
			Client?.Dispose();
			CronTimer?.Dispose();
		}

		private void AddToLaunchWithWindows()
		{
			using RegistryKey reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

			var ass = Assembly.GetExecutingAssembly();


			var location = Path.ChangeExtension(ass.Location, ".exe");

			reg.SetValue("Launch SBot", location);
		}
	}
}
