using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Learnest.Services;

namespace Learnest.Views
{
    /// <summary>Native file/folder pickers and the small confirmation dialogs used across views.</summary>
    internal static class Dialogs
    {
        /// <summary>
        /// The standard Windows "select folder" dialog.
        ///
        /// Two native routes are used, in order:
        ///   1. <c>Microsoft.Win32.OpenFolderDialog</c> (.NET 8 / Windows Desktop).  This is called
        ///      directly rather than through reflection: the earlier reflection lookup used
        ///      Type.GetType with an assembly-qualified name, which silently returns null when
        ///      PresentationFramework has not been loaded yet, so the app fell back to the built-in
        ///      tree picker and never showed the system dialog at all.
        ///   2. The shell item dialog (IFileDialog with FOS_PICKFOLDERS) through COM, which is the
        ///      same dialog Explorer uses and works on every supported Windows build.
        /// </summary>
        public static string PickFolder(Window owner, string title, string initialPath)
        {
            // Route 1: the WPF folder dialog.
            try
            {
                Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog();
                dialog.Title = string.IsNullOrEmpty(title) ? Loc.T("container.browse_folder") : title;
                dialog.Multiselect = false;
                if (!string.IsNullOrEmpty(initialPath) && System.IO.Directory.Exists(initialPath))
                {
                    dialog.InitialDirectory = initialPath;
                }
                if (owner != null && owner.IsLoaded)
                {
                    try { dialog.ShowDialog(owner); }
                    catch (InvalidOperationException) { dialog.ShowDialog(); }
                }
                else
                {
                    dialog.ShowDialog();
                }
                string selected = dialog.FolderName;
                if (!string.IsNullOrEmpty(selected)) { return selected; }

                // null means the user cancelled; do not silently open a second dialog.
                return null;
            }
            catch (Exception ex)
            {
                Program.Trace("OpenFolderDialog unavailable (" + ex.GetType().Name + "), using the shell dialog");
            }

            // Route 2: the Explorer folder picker through COM.
            string shellResult = ShellFolderPicker.Pick(owner, title, initialPath);
            if (shellResult != null) { return shellResult; }

            // Route 3 (last resort, e.g. COM unavailable): the built-in tree picker.
            Program.Trace("shell folder dialog unavailable, using the built-in tree picker");
            FolderPickerWindow picker = new FolderPickerWindow(title, initialPath);
            if (owner != null && owner.IsLoaded) { picker.Owner = owner; }
            bool? result = picker.ShowDialog();
            return result == true ? picker.SelectedPath : null;
        }

        public static string[] PickFiles(Window owner, string title, string filter, bool multiselect)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = title;
            dialog.Filter = string.IsNullOrEmpty(filter) ? Loc.T("common.files") + "|*.*" : filter;
            dialog.Multiselect = multiselect;
            dialog.CheckFileExists = true;
            bool? ok = owner != null && owner.IsLoaded ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            if (ok != true) { return new string[0]; }
            return dialog.FileNames;
        }

        public static string PickImage(Window owner)
        {
            string[] files = PickFiles(owner, Loc.T("settings.background_choose"),
                "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*", false);
            return files.Length > 0 ? files[0] : null;
        }

        public static string PickArchive(Window owner)
        {
            string[] files = PickFiles(owner, Loc.T("container.import_file"),
                "Learnest container (*.learnest.zip;*.zip)|*.learnest.zip;*.zip|All files|*.*", false);
            return files.Length > 0 ? files[0] : null;
        }

        public static string PickLanguagePack(Window owner)
        {
            string[] files = PickFiles(owner, Loc.T("settings.language_import"), "Language pack (*.json)|*.json", false);
            return files.Length > 0 ? files[0] : null;
        }

        /// <summary>Small prompt with a single text box.</summary>
        public static string PromptText(Window owner, string title, string label, string initial)
        {
            string result = null;
            TextBox input = UiKit.Input(initial, null);
            StackPanel content = new StackPanel();
            content.Children.Add(UiKit.Field(label, input, null));
            if (UiKit.ShowDialog(owner, title, content, Loc.T("common.ok"), Loc.T("common.cancel"),
                delegate
                {
                    if (string.IsNullOrEmpty(input.Text)) { return false; }
                    result = input.Text;
                    return true;
                }, false))
            {
                return result;
            }
            return null;
        }

        /// <summary>Three-way choice used when deleting a category that still holds todos.</summary>
        public enum CategoryDeleteChoice { Cancel, MoveToUncategorized, MoveToOther, DeleteTodos }

        public sealed class CategoryDeleteRequest
        {
            public CategoryDeleteChoice Choice = CategoryDeleteChoice.Cancel;
            public string TargetCategoryId;
        }

        public static CategoryDeleteRequest AskCategoryDeletion(Window owner, Data.Category category, int todoCount,
            List<Data.Category> otherCategories)
        {
            CategoryDeleteRequest request = new CategoryDeleteRequest();
            StackPanel content = new StackPanel();

            TextBlock prompt = UiKit.Body(Loc.T("tasks.category_delete_prompt", category.Name, todoCount));
            prompt.TextWrapping = TextWrapping.Wrap;
            prompt.Margin = new Thickness(0, 0, 0, 14);
            content.Children.Add(prompt);

            RadioButton moveUncategorized = new RadioButton();
            moveUncategorized.Content = Loc.T("tasks.category_delete_uncategorized");
            moveUncategorized.IsChecked = true;
            moveUncategorized.Margin = new Thickness(0, 0, 0, 6);
            content.Children.Add(moveUncategorized);

            RadioButton moveOther = new RadioButton();
            moveOther.Content = Loc.T("tasks.category_delete_move");
            content.Children.Add(moveOther);

            List<KeyValuePair<string, string>> choices = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < otherCategories.Count; i++)
            {
                Data.Category other = otherCategories[i];
                if (other.Id == category.Id) { continue; }
                choices.Add(new KeyValuePair<string, string>(other.Id, other.Name));
            }
            ComboBox target = UiKit.Combo(choices, choices.Count > 0 ? choices[0].Key : null);
            target.Margin = new Thickness(24, 4, 0, 10);
            target.IsEnabled = false;
            content.Children.Add(target);

            RadioButton deleteAll = new RadioButton();
            deleteAll.Content = Loc.T("tasks.category_delete_delete_all");
            deleteAll.Margin = new Thickness(0, 0, 0, 0);
            content.Children.Add(deleteAll);

            moveOther.Checked += delegate { target.IsEnabled = true; };
            moveUncategorized.Checked += delegate { target.IsEnabled = false; };
            deleteAll.Checked += delegate { target.IsEnabled = false; };

            if (choices.Count == 0)
            {
                moveOther.IsEnabled = false;
            }

            UiKit.ShowDialog(owner, Loc.T("tasks.category_delete_title"), content,
                Loc.T("common.confirm"), Loc.T("common.cancel"), delegate
                {
                    if (deleteAll.IsChecked == true)
                    {
                        request.Choice = CategoryDeleteChoice.DeleteTodos;
                        return true;
                    }
                    if (moveOther.IsChecked == true)
                    {
                        string targetId = UiKit.SelectedKey(target);
                        if (string.IsNullOrEmpty(targetId))
                        {
                            MessageBox.Show(owner, Loc.T("tasks.category_delete_move"), Loc.T("error.generic"),
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                        request.Choice = CategoryDeleteChoice.MoveToOther;
                        request.TargetCategoryId = targetId;
                        return true;
                    }
                    request.Choice = CategoryDeleteChoice.MoveToUncategorized;
                    return true;
                }, true);

            return request;
        }
    }

    /// <summary>
    /// The Explorer "select folder" dialog through the shell item dialog API (IFileDialog with
    /// FOS_PICKFOLDERS).  Used when the WPF folder dialog is not available, so users still get the
    /// native picker instead of a hand rolled tree.
    /// </summary>
    internal static class ShellFolderPicker
    {
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        private const uint SIGDN_FILESYSPATH = 0x80058000;

        private static readonly Guid CLSID_FileOpenDialog = new Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        /// <summary>Shows the native picker; returns null when the user cancels or COM fails.</summary>
        internal static string Pick(Window owner, string title, string initialPath)
        {
            object dialogObject = null;
            try
            {
                Type dialogType = Type.GetTypeFromCLSID(CLSID_FileOpenDialog);
                if (dialogType == null) { return null; }
                dialogObject = Activator.CreateInstance(dialogType);
                IFileDialog dialog = dialogObject as IFileDialog;
                if (dialog == null) { return null; }

                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);

                if (!string.IsNullOrEmpty(title)) { dialog.SetTitle(title); }
                dialog.SetOkButtonLabel(Loc.T("common.select_folder"));

                if (!string.IsNullOrEmpty(initialPath) && System.IO.Directory.Exists(initialPath))
                {
                    try
                    {
                        IShellItem start;
                        SHCreateItemFromParsingName(initialPath, IntPtr.Zero, typeof(IShellItem).GUID, out start);
                        if (start != null) { dialog.SetFolder(start); }
                    }
                    catch
                    {
                        // An unusable start folder must not stop the dialog from opening.
                    }
                }

                IntPtr parent = owner != null ? new System.Windows.Interop.WindowInteropHelper(owner).Handle : IntPtr.Zero;
                int hr = dialog.Show(parent);
                if (hr != 0) { return null; }        // cancelled (HRESULT_FROM_WIN32(ERROR_CANCELLED))

                IShellItem item;
                dialog.GetResult(out item);
                if (item == null) { return null; }
                string path;
                item.GetDisplayName(SIGDN_FILESYSPATH, out path);
                return path;
            }
            catch (Exception ex)
            {
                Program.Trace("ShellFolderPicker failed: " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
            finally
            {
                if (dialogObject != null && Marshal.IsComObject(dialogObject))
                {
                    try { Marshal.ReleaseComObject(dialogObject); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// Minimal folder browser used only when both native dialogs are unavailable.
    /// Presents drives/folders in a tree plus a path box.
    /// </summary>
    internal sealed class FolderPickerWindow : Window
    {
        private readonly TextBox _pathBox;
        private readonly TreeView _tree;

        public string SelectedPath { get; private set; }

        public FolderPickerWindow(string title, string initialPath)
        {
            Title = title;
            Width = 560;
            Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiKit.Brush("Brush.Surface");
            FontFamily = UiKit.UiFont;
            Style = UiKit.Style("LearnestWindow");

            Grid root = new Grid();
            root.Margin = new Thickness(16);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _pathBox = UiKit.Input(initialPath ?? "", "C:\\");
            root.Children.Add(UiKit.Field(Loc.T("container.path"), _pathBox, null));

            _tree = new TreeView();
            _tree.Margin = new Thickness(0, 4, 0, 10);
            Grid.SetRow(_tree, 1);
            PopulateDrives();
            root.Children.Add(_tree);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(buttons, 2);

            Button cancel = UiKit.Outlined(Loc.T("common.cancel"), delegate { DialogResult = false; });
            buttons.Children.Add(cancel);
            Button ok = UiKit.Primary(Loc.T("common.select_folder"), delegate
            {
                string path = _pathBox.Text;
                if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
                {
                    MessageBox.Show(this, Loc.T("container.invalid_folder"), Loc.T("error.generic"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SelectedPath = path;
                DialogResult = true;
            });
            ok.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            Content = root;
        }

        private void PopulateDrives()
        {
            try
            {
                string[] drives = System.IO.Directory.GetLogicalDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    TreeViewItem item = new TreeViewItem();
                    item.Header = drives[i];
                    item.Tag = drives[i];
                    item.Items.Add(new TreeViewItem());      // placeholder for lazy loading
                    item.Expanded += NodeExpanded;
                    item.Selected += NodeSelected;
                    _tree.Items.Add(item);
                }
            }
            catch
            {
            }
        }

        private void NodeSelected(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if (item != null && item.Tag is string) { _pathBox.Text = (string)item.Tag; }
        }

        private void NodeExpanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if (item == null || !(item.Tag is string)) { return; }
            if (item.Items.Count != 1) { return; }
            item.Items.Clear();
            string path = (string)item.Tag;
            try
            {
                string[] folders = System.IO.Directory.GetDirectories(path);
                for (int i = 0; i < folders.Length; i++)
                {
                    string name;
                    try { name = System.IO.Path.GetFileName(folders[i]); }
                    catch { name = folders[i]; }
                    if (string.IsNullOrEmpty(name)) { continue; }
                    TreeViewItem child = new TreeViewItem();
                    child.Header = name;
                    child.Tag = folders[i];
                    child.Items.Add(new TreeViewItem());
                    child.Expanded += NodeExpanded;
                    child.Selected += NodeSelected;
                    item.Items.Add(child);
                }
            }
            catch
            {
            }
        }
    }
}
