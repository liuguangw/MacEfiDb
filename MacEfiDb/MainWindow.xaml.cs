//定义是否为代码调试模式
#define APP_DEBUG 
//
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
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
#pragma warning disable
                    //防止代码阻塞
                    Task.Run(new Action(() =>
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            this.loading.tip.Content = "正在解压文件";
                            this.loading.ShowDialog();
                        });
                    }));
#pragma warning restore
                    await Task.Run(new Action(() =>
                    {
                        ZipFile.ExtractToDirectory(zipFilePath, this.dataPath);
                    }));
                    this.loading.Hide();
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
#pragma warning disable
            //显示模态框
            Task.Run(new Action(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.loading.tip.Content = "正在保存,请稍后";
                    this.loading.ShowDialog();
                });
            }));
#pragma warning restore
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
            this.loading.Hide();
            System.Windows.MessageBox.Show("保存【" + selectedItem.name + "】配置成功", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
        }

        private void AppShutdown(object sender, RoutedEventArgs e)
        {
            MessageBoxResult a = System.Windows.MessageBox.Show("确定要退出本程序吗?", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (a == MessageBoxResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void ShowAbout(object sender, RoutedEventArgs e)
        {
            AboutApp about = new AboutApp();
            about.Owner = this;
            about.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            about.ShowDialog();
        }

        //字节大小转文件大小提示
        private string getFileSize(ulong fileBytes)
        {
            if (fileBytes < 1024)
            {
                return fileBytes + "Byte";
            }
            else if (fileBytes >= 1024 && fileBytes < (1024 * 1024))
            {
                return ((decimal)fileBytes / 1024).ToString("F2") + "KB";
            }
            else if (fileBytes >= (1024 * 1024) && fileBytes < (1024 * 1024 * 1024))
            {
                return ((decimal)fileBytes / 1024 / 1024).ToString("F2") + "MB";
            }
            return "unknown";
        }

        /// <summary>
        /// 获取文件MD5值
        /// </summary>
        /// <param name="fileName">文件绝对路径</param>
        /// <returns>MD5值</returns>
        public string GetMD5HashFromFile(string fileName)
        {
            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open,FileAccess.Read);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }
        }

        private async void UpdateData(object sender, RoutedEventArgs e)
        {
            string url = @"https://github.com/liuguangw/MacEfiDb/raw/master/MacEfiDb/data.zip";
            string dataMd5Url = @"https://github.com/liuguangw/MacEfiDb/raw/master/README.md";
            string tmpFilePath = this.dataPath + @".zip.tmp";
            FileInfo tmpFileInfo = new FileInfo(tmpFilePath);
            if (tmpFileInfo.Exists)
            {
                tmpFileInfo.Delete();
            }
#pragma warning disable
            //显示模态框
            Task.Run(new Action(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.loading.tip.Content = "正在获取文件";
                    this.loading.ShowDialog();
                });
            }));
#pragma warning restore
            await Task.Run(new Action(() =>
            {
                //计算本地文件的MD5值
                string localDataMd5 = this.GetMD5HashFromFile(this.dataPath + @".zip");
                string remoteDataMd5;
                //
                HttpWebRequest request = null;
                HttpWebResponse response = null;
                Stream stream = null;
                FileStream fs = null;
                try
                {
                    //下载校验文件
                    request = WebRequest.Create(dataMd5Url) as HttpWebRequest;
                    response = request.GetResponse() as HttpWebResponse;
                    stream = response.GetResponseStream();
                    this.Dispatcher.Invoke(() =>
                    {
                        this.loading.tip.Content = "正在获取远程文件信息";
                    });
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        remoteDataMd5 = reader.ReadToEnd();
                    }
                    //MD5相同
                    if (localDataMd5 == remoteDataMd5)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            this.loading.Hide();
                            System.Windows.MessageBox.Show("本地data文件已经是最新版本");
                        });
                    }
                    else
                    //更新远程文件
                    {
                        request = WebRequest.Create(url) as HttpWebRequest;
                        response = request.GetResponse() as HttpWebResponse;
                        stream = response.GetResponseStream();
                        this.Dispatcher.Invoke(() =>
                        {
                            this.loading.tip.Content = "正在下载更新";
                        });
                        //写入文件
                        fs = new FileStream(tmpFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        byte[] bArr = new byte[1024];
                        int size = stream.Read(bArr, 0, bArr.Length);
                        ulong totalSize = 0;
                        while (size > 0)
                        {
                            fs.Write(bArr, 0, size);
                            //更新下载数据的界面
                            totalSize += (ulong)size;
                            this.Dispatcher.Invoke(() =>
                            {
                                this.loading.tip.Content = "已下载数据:" + this.getFileSize(totalSize);
                            });
                            //
                            size = stream.Read(bArr, 0, bArr.Length);
                        }
                        //校验MD5
                        string downloadMd5 = this.GetMD5HashFromFile(tmpFilePath);
                        if (downloadMd5 != remoteDataMd5)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                this.loading.Hide();
                                System.Windows.MessageBox.Show("下载的临时文件校验失败\r\n远程MD5:" + remoteDataMd5 + "\r\n临时文件md5:" + downloadMd5, "MD5校验失败", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        else
                        {
                            //删除本地data目录、zip文件
                            this.Dispatcher.Invoke(() =>
                            {
                                this.loading.tip.Content = "正在清理本地数据";
                            });
                            DirectoryInfo dataPathInfo = new DirectoryInfo(this.dataPath);
                            if (dataPathInfo.Exists)
                            {
                                dataPathInfo.Delete(true);
                            }
                            //copy临时文件到data.zip
                            tmpFileInfo.CopyTo(this.dataPath + @".zip", true);
                            tmpFileInfo.Delete();
                            this.Dispatcher.Invoke(() =>
                        {
                            this.loading.Hide();
                            string result = "资源文件更新成功";
                            System.Windows.MessageBox.Show(result, "", MessageBoxButton.OK, MessageBoxImage.Information);
                            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                            System.Windows.MessageBox.Show(Process.GetCurrentProcess().MainModule.FileName, "", MessageBoxButton.OK, MessageBoxImage.Information);
                            System.Windows.Application.Current.Shutdown();
                        });
                        }
                        //end MD5校验
                    }//end更新远程文件
                }
                catch (Exception e1)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.loading.Hide();
                        System.Windows.MessageBox.Show(e1.Message, "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    //资源回收
                    if (fs != null)
                    {
                        fs.Close();
                    }
                    if (stream != null)
                    {
                        stream.Close();
                    }
                    if (response != null)
                    {
                        response.Close();
                    }
                }
            }));
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
