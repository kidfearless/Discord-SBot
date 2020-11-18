using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscordBotWPF.Models
{
	[Serializable]
	struct Commit
	{
        public int ID;
        public string Repo;
        public string Branch;
        public string Changeset;
        public string Created;
        public string Message;
        public User User;
	}
	[Serializable]
	struct User
	{
		public string Name;
		public string Avatar;
	}

	internal class CommitComparer : IEqualityComparer<Commit>
	{
		public bool Equals(Commit x, Commit y) => x.ID == y.ID;

		public int GetHashCode(Commit obj) => obj.ID;
	}

	internal class CommitSorter : IComparer<Commit>
	{
		public int Compare([AllowNull] Commit x, [AllowNull] Commit y)
		{
			DateTime.TryParse(x.Created, out DateTime xTime);
			DateTime.TryParse(y.Created, out DateTime yTime);
			return DateTime.Compare(xTime, yTime);
		}
	}
}
