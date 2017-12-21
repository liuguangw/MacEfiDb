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
        private Loading loadingDialog;
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns>获取资源压缩包保存路径</returns>
        private string getZipFilePath()
        {
            return this.dataPath + @".zip";
        }

        //创建并显示加载框
        private void showLoadingDialog(string str)
        {
#pragma warning disable
            Task.Run(new Action(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.loadingDialog = new Loading();
                    this.loadingDialog.Owner = this;
                    this.loadingDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    this.loadingDialog.tip.Content = str;
                    this.loadingDialog.ShowDialog();
                });
            }));
#pragma warning restore
        }

        //更新加载框中的文字
        private void updateLoadingDialog(string str)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.loadingDialog.tip.Content = str;
            });
        }

        //隐藏加载框
        private void hideLoadingDialog()
        {
            if (this.loadingDialog != null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.loadingDialog.Close();
                    this.loadingDialog = null;
                });
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
                string zipFilePath = this.getZipFilePath();
                FileInfo zipFileInfo = new FileInfo(zipFilePath);
                if (!zipFileInfo.Exists)
                {
                    System.Windows.MessageBox.Show("资源包:" + zipFilePath + "不存在", "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    this.showLoadingDialog("正在解压文件");
                    await Task.Run(new Action(() =>
                    {
                        ZipFile.ExtractToDirectory(zipFilePath, this.dataPath);
                    }));
                    this.hideLoadingDialog();
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
            this.showLoadingDialog("正在保存,请稍后");
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
            this.hideLoadingDialog();
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
        private string getFileSize(decimal fileBytes)
        {
            if (fileBytes < 1024)
            {
                return fileBytes + "Byte";
            }
            else if (fileBytes >= 1024 && fileBytes < (1024 * 1024))
            {
                return (fileBytes / 1024).ToString("F2") + "KB";
            }
            else if (fileBytes >= (1024 * 1024) && fileBytes < (1024 * 1024 * 1024))
            {
                return (fileBytes / 1024 / 1024).ToString("F2") + "MB";
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
                FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read);
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

        private string getRemoteFileContent(string url)
        {

            HttpWebRequest request = null;
            HttpWebResponse response = null;
            Stream stream = null;
            string fileContent = "";
            try
            {
                request = WebRequest.Create(url) as HttpWebRequest;
                response = request.GetResponse() as HttpWebResponse;
                stream = response.GetResponseStream();
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    fileContent = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
                if (response != null)
                {
                    response.Close();
                }
            }
            return fileContent;
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="url">文件的URL地址</param>
        /// <param name="savePath">文件的保存路径</param>
        /// <param name="onFileSizeChange">当文件大小发生改变时执行</param>
        /// <param name="onDownloadSuccess">当文件下载成功时执行</param>
        private void downloadFile(string url, string savePath, Action<ulong> onFileSizeChange, Action onDownloadSuccess)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            Stream stream = null;
            FileStream fs = null;
            try
            {
                //如果文件存在则删除文件
                FileInfo savePathInfo = new FileInfo(savePath);
                if (savePathInfo.Exists)
                {
                    savePathInfo.Delete();
                }
                //
                request = WebRequest.Create(url) as HttpWebRequest;
                response = request.GetResponse() as HttpWebResponse;
                stream = response.GetResponseStream();
                //写入文件
                fs = new FileStream(savePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                byte[] bArr = new byte[1024];
                int size = stream.Read(bArr, 0, bArr.Length);
                ulong totalSize = 0;
                while (size > 0)
                {
                    fs.Write(bArr, 0, size);
                    //更新下载数据的界面
                    totalSize += (ulong)size;
                    onFileSizeChange(totalSize);
                    //
                    size = stream.Read(bArr, 0, bArr.Length);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
                if (response != null)
                {
                    response.Close();
                }
                if (fs != null)
                {
                    fs.Close();
                }
            }
            //文件下载完成
            onDownloadSuccess();
        }

        private async void UpdateData(object sender, RoutedEventArgs e)
        {
            string url = @"https://github.com/liuguangw/MacEfiDb/raw/master/MacEfiDb/data.zip";
            string dataMd5Url = @"https://github.com/liuguangw/MacEfiDb/raw/master/data_md5.txt";
            string zipFilePath = this.getZipFilePath();
            string tmpFilePath = zipFilePath + @".tmp";
            this.showLoadingDialog("正在获取文件");
            await Task.Run(new Action(() =>
            {
                //计算本地文件的MD5值
                string localDataMd5 = this.GetMD5HashFromFile(zipFilePath);
                this.updateLoadingDialog("正在获取远程文件信息");
                string remoteDataMd5 = "";
                try
                {
                    remoteDataMd5 = this.getRemoteFileContent(dataMd5Url);
                }
                catch (Exception ex)
                {
                    this.hideLoadingDialog();
                    this.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(ex.Message, "获取校验文件失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                if (remoteDataMd5 == "")
                {
                    //获取md5文件失败
                }
                else if (localDataMd5 == remoteDataMd5)
                {
                    //文件md5值一致
                    this.hideLoadingDialog();
                    this.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("本地data文件已经是最新版本");
                    });
                }
                else
                {
                    //执行更新程序
                    this.updateLoadingDialog("正在下载更新");
                    //start downloadFile
                    try
                    {
                        this.downloadFile(url, tmpFilePath, (ulong totalSize) =>
                        {

                            this.updateLoadingDialog("已下载数据:" + this.getFileSize(totalSize));
                        }, () =>
                        {
                            //下载完成后校验MD5
                            string downloadMd5 = this.GetMD5HashFromFile(tmpFilePath);
                            if (downloadMd5 != remoteDataMd5)
                            {
                                this.hideLoadingDialog();
                                this.Dispatcher.Invoke(() =>
                                {
                                    System.Windows.MessageBox.Show("下载的临时文件校验失败\r\n远程MD5:" + remoteDataMd5 + "\r\n临时文件MD5:" + downloadMd5, "MD5校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                                });
                            }
                            else
                            {
                                //copy文件
                                this.copyFile(tmpFilePath, zipFilePath);
                                //删除临时文件
                                FileInfo tmpFileInfo = new FileInfo(tmpFilePath);
                                tmpFileInfo.Delete();
                                //删除data目录
                                DirectoryInfo dataPathInfo = new DirectoryInfo(this.dataPath);
                                if (dataPathInfo.Exists)
                                {
                                    dataPathInfo.Delete(true);
                                }
                                //重启应用程序
                                this.hideLoadingDialog();
                                this.Dispatcher.Invoke(() =>
                                {
                                    System.Windows.MessageBox.Show("资源文件更新成功", "", MessageBoxButton.OK, MessageBoxImage.Information);
                                    Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                                    System.Windows.Application.Current.Shutdown();
                                });
                            }
                        });
                    }
                    catch (Exception e1)
                    {
                        this.hideLoadingDialog();
                        this.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(e1.Message, "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    //end downloadFile
                }//end else

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
