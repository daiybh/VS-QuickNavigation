﻿
namespace VS_QuickNavigation
{
	//using Microsoft.VisualStudio.Shell;
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Threading;
	using System.ComponentModel;
	using System.Linq;
	using VS_QuickNavigation.Data;
	using System.Collections;

	public partial class QuickFileToolWindowControl : UserControl, INotifyPropertyChanged
	{
		private QuickFileToolWindow mQuickFileToolWindow;

		private bool mHistoryOnly;

		private CancellationTokenSource mToken;

		public event PropertyChangedEventHandler PropertyChanged;

		public string FileHeader
		{
			get
			{
				return "Files (" + Common.Instance.SolutionWatcher.FilesCount + ")";
			}
		}

		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler handler = this.PropertyChanged;
			if (handler != null)
			{
				var e = new PropertyChangedEventArgs(propertyName);
				handler(this, e);
			}
		}

		public QuickFileToolWindowControl(QuickFileToolWindow oQuickFileToolWindow, bool bHistoryOnly)
		{
			this.InitializeComponent();

			mQuickFileToolWindow = oQuickFileToolWindow;

			mQuickFileToolWindow.Closing += OnClosing;

			mHistoryOnly = bHistoryOnly;

			//Common.Instance.SolutionWatcher.RefreshFileList();
			Common.Instance.SolutionWatcher.OnFilesChanged += OnFilesChanged;

			DataContext = this;

			textBox.Focus();

			listView.SelectedIndex = 0;

			RefreshList();
		}

		private void OnClosing(object sender, CancelEventArgs e)
		{
			Common.Instance.SolutionWatcher.OnFilesChanged -= OnFilesChanged;
		}

		private void OnFilesChanged()
		{
			OnPropertyChanged("FileHeader");
			Dispatcher.BeginInvoke(new Action(RefreshList));
		}

		private void textBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.IsUp)
			{
				if (e.Key == System.Windows.Input.Key.Up)
				{
					if (listView.SelectedIndex > 0)
					{
						listView.SelectedIndex--;
						listView.ScrollIntoView(listView.SelectedItem);
					}
					e.Handled = true;
				}
				else if (e.Key == System.Windows.Input.Key.Down)
				{
					if (listView.SelectedIndex == -1)
					{
						listView.SelectedIndex = 1;
					}
					else
					{
						listView.SelectedIndex++;
					}
					listView.ScrollIntoView(listView.SelectedItem);
					e.Handled = true;
				}
			}
			else if (e.IsDown)
			{
				if (e.Key == System.Windows.Input.Key.Return)
				{
					OpenCurrentSelection();
				}
				else if (e.Key == System.Windows.Input.Key.Escape)
				{
					mQuickFileToolWindow.Close();
				}
			}
		}

		private void textBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshList();
		}

		private void listView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.IsDown)
			{
				if (e.Key == System.Windows.Input.Key.Return)
				{
					OpenCurrentSelection();
				}
				else if (e.Key == System.Windows.Input.Key.Escape)
				{
					mQuickFileToolWindow.Close();
				}
			}
		}

		private void RefreshList()
		{
			if (null != mToken)
			{
				mToken.Cancel();
			}
			mToken = new CancellationTokenSource();
			string sSearch = textBox.Text;
			Task.Run(() =>
			{
				try
				{
					//Common.Instance.SolutionWatcher.SetNeedRefresh();
					ParallelQuery<FileData> source = null;
					if (mHistoryOnly || string.IsNullOrWhiteSpace(sSearch))
					{
						source = Common.Instance.SolutionWatcher.Files
						.AsParallel()
						.WithCancellation(mToken.Token)
						.Where(fileData => fileData.Status == FileStatus.Recent)
						;
						
					}
					else
					{
						string[] exts = Common.Instance.Settings.ListedExtensions;

						source = Common.Instance.SolutionWatcher.Files
						.AsParallel()
						.WithCancellation(mToken.Token)
						.Where(fileData => exts.Any(ext => fileData.File.EndsWith(ext)))
						;
					}

					int total = source.Count();

					IEnumerable<SearchResult<FileData>> results = source
						.Select(fileData => new SearchResult<FileData>(fileData, sSearch, fileData.Path, "\\"))
						;

					if (!string.IsNullOrWhiteSpace(sSearch))
					{
						int searchStringLen = sSearch.Length;
						results = results.Where(resultData => resultData.SearchScore > searchStringLen);
					}

					results = results.OrderByDescending(fileData => fileData.SearchScore) // Sort by score
						.ThenByDescending(fileData => fileData.Data.RecentIndex) // Sort by last access
						;

					int count = results.Count();

					Action<IEnumerable> setMethod = (res) =>
					{
						listView.ItemsSource = res;
						CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
						view.GroupDescriptions.Clear();

						if (string.IsNullOrWhiteSpace(sSearch))
						{
							PropertyGroupDescription groupDescription = new PropertyGroupDescription("Data.StatusString");
							//groupDescription.GroupNames.Add(FileStatus.Recent.ToString());
							/*foreach (string status in Enum.GetNames(typeof(FileStatus)))
							{
								groupDescription.GroupNames.Add(status);
							}*/

							view.GroupDescriptions.Add(groupDescription);
						}

						string title = mQuickFileToolWindow.Title;
						int pos = title.IndexOf(" [");
						if (pos != -1)
						{
							title = title.Substring(0, pos);
						}
						mQuickFileToolWindow.Title = title + " [" + count + "/" + total + "]";
					};
					
					Dispatcher.BeginInvoke(setMethod, results.ToList());
					//Dispatcher.BeginInvoke(setMethod, results.AsEnumerable());
				}
				catch (Exception e) { }
				
			});
		}

		private void listView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			OpenCurrentSelection();
		}

		private void OpenCurrentSelection()
		{
			int selectedIndex = listView.SelectedIndex;
			if (selectedIndex == -1) selectedIndex = 0;
			SearchResult<FileData> results = listView.Items[selectedIndex] as SearchResult<FileData>;
			Common.Instance.DTE2.ItemOperations.OpenFile(results.Data.Path);
            mQuickFileToolWindow.Close();
		}
	}
}