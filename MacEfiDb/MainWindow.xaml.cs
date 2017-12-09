using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace MacEfiDb
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string appPath;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.appPath = System.AppDomain.CurrentDomain.BaseDirectory;
            bool debug = true;
            if (debug)
            {
                DirectoryInfo di = new DirectoryInfo(this.appPath);
                this.appPath = di.Parent.Parent.FullName;
            }
            using (StreamReader reader = File.OpenText(this.appPath + @"\config\common.json"))
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
            if (srcDirInfo.Exists) {
                if (!distDirInfo.Exists) {
                    distDirInfo.Create();
                }
                FileInfo[] files = srcDirInfo.GetFiles();
                DirectoryInfo[] dirs = srcDirInfo.GetDirectories();
                int i;
                for (i = 0; i < dirs.Length; i++)
                {
                    this.copyDir(srcDir+"\\"+ dirs[i].Name,distDir+"\\"+dirs[i].Name);
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
            if (srcFileInfo.Exists) {
                DirectoryInfo distDirInfo = distFileInfo.Directory;
                if (!distDirInfo.Exists) {
                    distDirInfo.Create();
                }
                //copy
                srcFileInfo.CopyTo(distFileInfo.FullName,true);
            }
        }

        private void saveConfig(object sender, RoutedEventArgs e)
        {
            if (this.savePath == null)
            {
                System.Windows.MessageBox.Show("请选择保存目录", "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            //判断EFI目录是否存在
            DirectoryInfo saveDirInfo = new DirectoryInfo(this.savePath+@"\EFI");
            if (saveDirInfo.Exists) {
                MessageBoxResult a = System.Windows.MessageBox.Show("目标目录已存在EFI文件夹,是否删除?", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (a == MessageBoxResult.Yes)
                {
                    saveDirInfo.Delete(true);
                }
                else {
                    return;
                }
            }
            saveBtn.Content = "正在保存...";
            saveBtn.IsEnabled = false;
            ConfigItem selectedItem = configSelector.SelectedItem as ConfigItem;
            int i, targetType;
            //copy文件
            JArray fileList = optionFiles[selectedItem.file] as JArray;
            for (i = 0; i < fileList.Count; i++)
            {
                targetType = (int)fileList[i]["type"];
                switch (targetType)
                {
                    case ACTION_COPY_DIR:
                        this.copyDir(this.appPath + "\\" + (string)fileList[i]["src"], this.savePath + "\\" + (string)fileList[i]["dist"]);
                        break;
                    case ACTION_COPY_FILE:
                        this.copyFile(this.appPath + "\\" + (string)fileList[i]["src"], this.savePath + "\\" + (string)fileList[i]["dist"]);
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
            this.copyFile(this.appPath + @"\config\plist\" + selectedItem.plist+".plist", this.savePath + @"\EFI\CLOVER\config.plist");
            saveBtn.Content = "保存配置";
            saveBtn.IsEnabled = true;
            System.Windows.MessageBox.Show("保存【"+selectedItem.name+"】配置成功","操作成功",MessageBoxButton.OK,MessageBoxImage.Information,MessageBoxResult.OK);
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
