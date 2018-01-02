﻿
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;

namespace VS_QuickNavigation
{
	internal sealed class QuickHistoryCommand
	{
		public const int CommandId = 0x0101;
		public static readonly Guid CommandSet = new Guid("ad64a987-3060-494b-94c1-07bab75f9da3");

		private readonly Package package;
		private QuickFileToolWindow window;
		
		private QuickHistoryCommand(Package package)
		{
			if (package == null)
			{
				throw new ArgumentNullException("package");
			}

			this.package = package;

			OleMenuCommandService commandService = Common.Instance.GetService<IMenuCommandService>() as OleMenuCommandService;
			if (commandService != null)
			{
				var menuCommandID = new CommandID(CommandSet, CommandId);
				var menuItem = new MenuCommand(this.ShowWindow, menuCommandID);
				commandService.AddCommand(menuItem);
			}

			window = new QuickFileToolWindow(true);
		}
		
		public static QuickHistoryCommand Instance
		{
			get;
			private set;
		}

		public static void Initialize(Package package)
		{
			Instance = new QuickHistoryCommand(package);
		}

		private void ShowWindow(object sender, EventArgs e)
		{
			window.OpenDialog();
		}
	}
}