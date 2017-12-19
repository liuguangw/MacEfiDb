//定义是否为代码调试模式
#define APP_DEBUG 
//
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace MacEfiDb
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Loading loading;
        //
        private string dataPath;
        private string savePath = null;
        //json配置
        private JObject jsonConfig;
        private JArray optionList;
        private JObject optionFiles;
        //常量
        public const int ACTION_COPY_DIR = 0;
        public const int ACTION_COPY_FILE = 1;
        public const int ACTION_DELETE_FILE = 2;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //loading tip
            this.loading = new Loading();
            this.loading.Owner = this;
            this.loading.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            //
            string basePath = System.AppDomain.CurrentDomain.BaseDirectory;
#if (APP_DEBUG)

            DirectoryInfo di = new DirectoryInfo(basePath);
            basePath = di.Parent.Parent.FullName + @"\";
#endif
            this.dataPath = basePath + @"data";
            //判断data目录是否存在
            DirectoryInfo dataPathInfo = new DirectoryInfo(this.dataPath);
            if (!dataPathInfo.Exists)
            {
                string zipFilePath = this.dataPath + @".zip";
                FileInfo zipFileInfo = new FileInfo(zipFilePath);
                if (!zipFileInfo.Exists)
                {
                    System.Windows.MessageBox.Show("资源包:" + zipFilePath + "不存在", "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    this.Hide();
                    this.loading.tip.Content = "正在解压文件";
                    this.loading.Show();
                    await Task.Run(new Action(() =>
                    {
                        ZipFile.ExtractToDirectory(zipFilePath, this.dataPath);
                    }));
                    this.loading.Hide();
                    this.Show();
                    dataPathInfo.Refresh();
                }
            }
            if (dataPathInfo.Exists)
            {
                using (StreamReader reader = File.OpenText(this.dataPath + @"\config\common.json"))
                {
                    this.jsonConfig = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                    this.optionList = (JArray)this.jsonConfig["option_list"];
                    this.optionFiles = (JObject)this.jsonConfig["option_files"];
                    int i = 0;
                    for (i = 0; i < optionList.Count; i++)
                    {
                        configSelector.Items.Add(new ConfigItem((string)optionList[i]["name"], (string)optionList[i]["file"], (string)optionList[i]["plist"]));
                    }
                    configSelector.SelectedIndex = 0;
                }
            }
        }

        private void chooseFolder(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.savePath = folderBrowserDialog.SelectedPath;
                fileInput.Text = this.savePath;
            }
        }

        private void copyDir(string srcDir, string distDir)
        {
            DirectoryInfo srcDirInfo = new DirectoryInfo(srcDir);
            DirectoryInfo distDirInfo = new DirectoryInfo(distDir);
            if (srcDirInfo.Exists)
            {
                if (!distDirInfo.Exists)
                {
                    distDirInfo.Create();
                }
                FileInfo[] files = srcDirInfo.GetFiles();
                DirectoryInfo[] dirs = srcDirInfo.GetDirectories();
                int i;
                for (i = 0; i < dirs.Length; i++)
                {
                    this.copyDir(srcDir + "\\" + dirs[i].Name, distDir + "\\" + dirs[i].Name);
                }
                for (i = 0; i < files.Length; i++)
                {
                    this.copyFile(srcDir + "\\" + files[i].Name, distDir + "\\" + files[i].Name);
                }
            }
        }

        private void copyFile(string srcPath, string distPath)
        {

            FileInfo srcFileInfo = new FileInfo(srcPath);
            FileInfo distFileInfo = new FileInfo(distPath);
            if (srcFileInfo.Exists)
            {
                DirectoryInfo distDirInfo = distFileInfo.Directory;
                if (!distDirInfo.Exists)
                {
                    distDirInfo.Create();
                }
                //copy
                srcFileInfo.CopyTo(distFileInfo.FullName, true);
            }
        }

        private async void saveConfig(object sender, RoutedEventArgs e)
        {
            if (configSelector.SelectedIndex == -1)
            {
                System.Windows.MessageBox.Show("请选择配置", "", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (this.savePath == null)
            {
                System.Windows.MessageBox.Show("请选择保存目录", "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            //判断EFI目录是否存在
            DirectoryInfo saveDirInfo = new DirectoryInfo(this.savePath + @"\EFI");
            if (saveDirInfo.Exists)
            {
                MessageBoxResult a = System.Windows.MessageBox.Show("目标目录已存在EFI文件夹,是否删除?", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (a == MessageBoxResult.Yes)
                {
                    saveDirInfo.Delete(true);
                }
                else
                {
                    return;
                }
            }
            this.IsEnabled = false;
            this.loading.tip.Content = "正在保存,请稍后";
            this.loading.Show();
            ConfigItem selectedItem = configSelector.SelectedItem as ConfigItem;
            int i, targetType;
            //copy文件
            JArray fileList = optionFiles[selectedItem.file] as JArray;
            await Task.Run(new Action(() =>
            {
                for (i = 0; i < fileList.Count; i++)
                {
                    targetType = (int)fileList[i]["type"];
                    switch (targetType)
                    {
                        case ACTION_COPY_DIR:
                            this.copyDir(this.dataPath + "\\" + (string)fileList[i]["src"], this.savePath + "\\" + (string)fileList[i]["dist"]);
                            break;
                        case ACTION_COPY_FILE:
                            this.copyFile(this.dataPath + "\\" + (string)fileList[i]["src"], this.savePath + "\\" + (string)fileList[i]["dist"]);
                            break;
                        case ACTION_DELETE_FILE:
                            FileInfo tmpFile = new FileInfo(this.savePath + "\\" + (string)fileList[i]["path"]);
                            if (tmpFile.Exists)
                            {
                                tmpFile.Delete();
                            }
                            break;
                    }
                }
                //copy配置
                this.copyFile(this.dataPath + @"\config\plist\" + selectedItem.plist + ".plist", this.savePath + @"\EFI\CLOVER\config.plist");
            }));
            this.IsEnabled = true;
            this.loading.Hide();
            System.Windows.MessageBox.Show("保存【" + selectedItem.name + "】配置成功", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
        }
    }

    public class ConfigItem
    {
        public string name { get; }
        public string file { get; }
        public string plist { get; }

        public ConfigItem(string name, string file, string plist)
        {
            this.name = name;
            this.file = file;
            this.plist = plist;
        }

        public override string ToString()
        {
            return this.name;
        }
    }
}
