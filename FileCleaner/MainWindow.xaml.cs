using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FileCleaner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Ctor

        public MainWindow()
        {
            InitializeComponent();
            PopulateConnectionInfos();
        }

        #endregion
        #region Regex

        private string regex =
                @"^" +                          //# Start of line
                @"(?<dir>[\-ld])" +             //# File size          
                @"(?<permission>[\-rwx]{9})" +  //# Whitespace          \n
                @"\s+" +                        //# Whitespace          \n
                @"(?<filecode>\d+)" +
                @"\s+" +                        //# Whitespace          \n
                @"(?<owner>\w+)" +
                @"\s+" +                        //# Whitespace          \n
                @"(?<group>\w+)" +
                @"\s+" +                        //# Whitespace          \n
                @"(?<size>\d+)" +
                @"\s+" +                        //# Whitespace          \n
                @"(?<month>\w{3})" +            //# Month (3 letters)   \n
                @"\s+" +                        //# Whitespace          \n
                @"(?<day>\d{1,2})" +            //# Day (1 or 2 digits) \n
                @"\s+" +                        //# Whitespace          \n
                @"(?<timeyear>[\d:]{4,5})" +    //# Time or year        \n
                @"\s+" +                        //# Whitespace          \n
                @"(?<filename>(.*))" +          //# Filename            \n
                @"$";                           //# End of line

        #endregion

        HashSet<string> ignoredFileTypeList;

        #region Events 

        private void btnList_Click(object sender, RoutedEventArgs e)
        {
            string uri = txtHost.Text;
            string username = txtUserName.Text;
            string password = txtPassword.Password;
            string ignoredFileTypes = txtIgnoredFileTypes.Text;
            ignoredFileTypeList = new HashSet<string>(ignoredFileTypes.Split(",", StringSplitOptions.RemoveEmptyEntries));
            treeViewFileName.Items.Clear();
            try
            {
                var allItems = ListFtpDirectory(uri, new NetworkCredential(username, password));
                foreach (var item in allItems)
                {
                    treeViewFileName.Items.Add(item);
                }
                //After Succesfull List 
                SaveConnectionInfos();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "List Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void btnDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            var userAnswer = MessageBox.Show("All files will be deleted in current path! Are you sure", "Are you sure?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (userAnswer != MessageBoxResult.OK)
                return;


            string uri = txtHost.Text;
            listForWillDelete.Items.Clear();

            foreach (TreeViewItem item in treeViewFileName.Items)
            {
                DeleteFilesRecursive(item, uri);
            }
        }

        private void chkRememberMe_Checked(object sender, RoutedEventArgs e)
        {
            SaveConnectionInfos();
        }
        private void chkRememberMe_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!chkRememberMe.IsChecked.HasValue || !(bool)chkRememberMe.IsChecked)
            {
                Properties.Settings.Default.RememberMe = false;
                Properties.Settings.Default.Host = string.Empty;
                Properties.Settings.Default.UserName = string.Empty;
                Properties.Settings.Default.IgnoredFileTypes = string.Empty;
                Properties.Settings.Default.Save();
            }
        }

        #endregion

        List<TreeViewItem> ListFtpDirectory(string url, NetworkCredential credentials)
        {

            List<TreeViewItem> items = new List<TreeViewItem>();
            List<string> lines = new List<string>();

            #region Get Lines

            WebRequest listRequest = WebRequest.Create(url);
            listRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            listRequest.Credentials = credentials;
            using (WebResponse listResponse = listRequest.GetResponse())
            using (Stream listStream = listResponse.GetResponseStream())
            using (StreamReader listReader = new StreamReader(listStream))
            {
                while (!listReader.EndOfStream)
                {
                    string line = listReader.ReadLine();
                    lines.Add(line);
                }
            }

            #endregion

            foreach (string line in lines)
            {
                var split = new Regex(regex).Match(line);
                string fileName = split.Groups["filename"].ToString();
                string dir = split.Groups["dir"].ToString();
                bool isDirectory = !string.IsNullOrWhiteSpace(dir) && dir.Equals("d", StringComparison.OrdinalIgnoreCase);

                var item = new TreeViewItem() { Header = fileName };
                if (isDirectory)
                {
                    string fileUrl = url + fileName + "/";
                    var subItems = ListFtpDirectory(fileUrl, credentials);
                    //dont show empty file
                    if (!subItems.Any())
                        continue;
                    foreach (var subItem in subItems)
                        item.Items.Add(subItem);
                }
                else
                {
                    //Skip ignored files
                    if (IsIgnoredFileType(fileName))
                        continue;

                    item.IsSelected = true;
                }
                items.Add(item);
            }
            return items;
        }

        private bool IsIgnoredFileType(string fileName)
        {
            foreach (var ignoredFileType in ignoredFileTypeList)
            {
                if (fileName.EndsWith(ignoredFileType))
                    return true;
            }
            return false;
        }
        #region For Settings
        private void SaveConnectionInfos()
        {
            if (chkRememberMe.IsChecked.HasValue && (bool)chkRememberMe.IsChecked)
            {
                Properties.Settings.Default.RememberMe = true;
                Properties.Settings.Default.Host = txtHost.Text;
                Properties.Settings.Default.UserName = txtUserName.Text;
                Properties.Settings.Default.IgnoredFileTypes = txtIgnoredFileTypes.Text;
                Properties.Settings.Default.Save();
            }
        }
        private void PopulateConnectionInfos()
        {
            try
            {
                if (Properties.Settings.Default.RememberMe)
                {
                    txtHost.Text = Properties.Settings.Default.Host;
                    txtUserName.Text = Properties.Settings.Default.UserName;
                    txtIgnoredFileTypes.Text = Properties.Settings.Default.IgnoredFileTypes;
                    chkRememberMe.IsChecked = Properties.Settings.Default.RememberMe;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Can not load default values", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
        #region For Delete 

        public void DeleteFilesRecursive(TreeViewItem treeViewItem, string url)
        {
            //if exist sub item
            foreach (TreeViewItem item in treeViewItem.Items)
            {
                string fileUrl = url + treeViewItem.Header + "/";
                if (item.IsSelected)
                {//if is not directory then delete file
                    DeleteFile(fileUrl + item.Header);
                }
                else
                { //if is directory then recursive
                    DeleteFilesRecursive(item, fileUrl);
                }
            }
            //if is not directory then delete file
            if (treeViewItem.IsSelected && treeViewItem.Items.Count == 0)
            {
                DeleteFile(url + treeViewItem.Header);
            }
        }

        private void DeleteFile(string fileUrl)
        {
            string username = txtUserName.Text;
            string password = txtPassword.Password;
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileUrl);
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(username, password);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    string message = fileUrl + " Status: " + response.StatusDescription;
                    listForWillDelete.Items.Add(new ListViewItem()
                    {
                        Content = message,
                        Foreground = new SolidColorBrush(Colors.GreenYellow)
                    });
                }
            }
            catch (Exception ex)
            {
                string message = fileUrl + " Error on delete: " + ex.Message;
                listForWillDelete.Items.Add(new ListViewItem()
                {
                    Content = message,
                    Foreground = new SolidColorBrush(Colors.Red)
                });
            }
        }
        #endregion
    }
}
