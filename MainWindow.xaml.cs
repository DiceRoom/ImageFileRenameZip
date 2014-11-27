using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.ObjectModel;  
using Microsoft.Win32;
using WindowsForm = System.Windows.Forms;

namespace DL
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /************************************************************************/
        /* 基本処理                                                             */
        /************************************************************************/

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /************************************************************************/
        /* コールバック処理                                                     */
        /************************************************************************/

        //============================================================================
        //! ディレクトリ指定ボタンが押された時の処理
        private void _ClickLoadDirectory(object iSender, RoutedEventArgs iArgs)
        {
            var dialog = new WindowsForm.FolderBrowserDialog();
            dialog.Description = "フォルダを指定してください。";
            dialog.RootFolder = Environment.SpecialFolder.Desktop;
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog() == WindowsForm.DialogResult.OK)
            {
                mItemList.Clear();
                _AddDirectory(dialog.SelectedPath);
                DirectoryListView.ItemsSource = mItemList.ToArray();
            }
        }

        //============================================================================
        //! ファイル名変更
        private void _ClickFileRename(object iSender, RoutedEventArgs iArgs)
        {
            //未選択の場合は無視
            if(DirectoryListView.SelectedItems.Count == 0)
            {
                System.Media.SystemSounds.Hand.Play();
                MessageBox.Show("ディレクトリが未選択です", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //選択
            foreach (BindItem iItem in DirectoryListView.SelectedItems)
            {
                //一旦かぶらないようにリネームする
                string[] files = _GetImageFileList(iItem.FilePath);
                foreach (var iIndex in System.Linq.Enumerable.Range(0, files.Length))
                {
                    var rename_Str = System.IO.Path.GetDirectoryName(files[iIndex]);
                    rename_Str += String.Format(@"\RENAME_WAIT_{0:D3}.jpg", iIndex);
                    System.IO.File.Move(files[iIndex], rename_Str);
                    files[iIndex] = rename_Str;
                }

                //ちゃんとした名前に
                foreach (var iIndex in System.Linq.Enumerable.Range(0 , files.Length))
                {
                    var rename_Str = System.IO.Path.GetDirectoryName(files[iIndex]);
                    rename_Str += String.Format(@"\{0:D3}.jpg", iIndex);
                    System.IO.File.Move(files[iIndex], rename_Str);
                }
            }

            System.Media.SystemSounds.Asterisk.Play();
            MessageBox.Show("リネームに成功しました", "確認", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //============================================================================
        //! 圧縮処理
        private void _ClickZipFiles(object iSender, RoutedEventArgs iArgs)
        {
            //未選択の場合は無視
            if (DirectoryListView.SelectedItems.Count == 0)
            {
                System.Media.SystemSounds.Hand.Play();
                MessageBox.Show("ディレクトリが未選択です", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var window = new ZipDetailsSettingWindow();
            window.OKButton.Click += (iClickSender, iClickArgs) =>
            {
                //キーワードが無い場合エラーとする
                var keyword = "{NumKey}";
                var text = window.ZipFileTextBox.Text.Trim();
                if (text.IndexOf(keyword) == -1)
                {
                    System.Media.SystemSounds.Hand.Play();
                    MessageBox.Show(string.Format("キーワード\"{0}\"が存在しません", keyword), "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //キーワード以外の文字が未入力
                if (text.Length <= keyword.Length)
                {
                    System.Media.SystemSounds.Hand.Play();
                    MessageBox.Show("キーワード以外の文字列が未入力です", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //圧縮
                try
                {
                    _ZipSelectFiles(text);
                    System.Media.SystemSounds.Asterisk.Play();
                    MessageBox.Show("圧縮に成功しました", "確認", MessageBoxButton.OK, MessageBoxImage.Information);
                    window.Close();
                }
                catch (System.Exception iException)
                {
                    System.Media.SystemSounds.Hand.Play();
                    MessageBox.Show("圧縮に失敗しました\n{0}" + iException.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
            };
            window.ShowDialog();
        }

        /************************************************************************/
        /* 内部処理                                                             */
        /************************************************************************/

        //============================================================================
        //! ディレクトリの追加
        private void _AddDirectory(string iDirectoryPath)
        {
            //ディレクトリが無い場合はここをjpg一覧とみなす
            string[] dirs = System.IO.Directory.GetDirectories(iDirectoryPath);
            if(dirs.Length == 0)
            {
                string[] files = _GetImageFileList(iDirectoryPath);
                if(files.Length != 0)
                {
                    //追加
                    mItemList.Add(new BindItem()
                    {
                        FilePath = iDirectoryPath,
                        FileVal = files.Length
                    });  
                }
            }
            else
            {
                foreach (var iNextDirectoryPath in dirs)
                {
                    _AddDirectory(iNextDirectoryPath);
                }
            }
        }

        //============================================================================
        //! 選択されているファイルを圧縮する
        private void _ZipSelectFiles(string iZipFileName)
        {
            //出力先のフォルダがなければ作成
            if (!System.IO.Directory.Exists(@"Output"))
            {
                System.IO.Directory.CreateDirectory("Output");
            }

            //Tempフォルダがなければ作成
            if (!System.IO.Directory.Exists(@"Temp"))
            {
                System.IO.Directory.CreateDirectory("Temp");
            }

            //選択
            int index = 1;
            foreach (BindItem iItem in DirectoryListView.SelectedItems)
            {
                //Tempフォルダの中身を全削除
                foreach (var iFile in System.IO.Directory.GetFiles("Temp"))
                {
                    System.IO.File.Delete(iFile);
                }

                //Tempフォルダの中に指定フォルダのjpgファイルのみをコピー
                int file_Index = 0;
                foreach (var iFile in _GetImageFileList(iItem.FilePath))
                {
                    var extension = System.IO.Path.GetExtension(iFile);
                    var file_Name = String.Format(@"{0:D3}{1}", file_Index, extension);
                    System.IO.File.Copy(iFile, @"Temp\" + file_Name);
                    ++file_Index;
                }
                

                string zip_File_Path = @"Output\" + iZipFileName + ".zip";
                zip_File_Path = zip_File_Path.Replace("{NumKey}", index.ToString());

                var fast_Zip = new ICSharpCode.SharpZipLib.Zip.FastZip();
                fast_Zip.CreateZip(zip_File_Path, @"Temp\", false, null, null);

                ++index;
            }
        }

        //============================================================================
        //! 指定フォルダの画像ファイル一覧を取得する
        private string[] _GetImageFileList(string iDirectoryPath)
        {
            var jpg_List = new List<string>(System.IO.Directory.GetFiles(iDirectoryPath, "*.jpg"));
            var png_List = new List<string>(System.IO.Directory.GetFiles(iDirectoryPath, "*.png"));
            jpg_List.AddRange(png_List);

            var image_List = jpg_List.ToArray();
            Array.Sort(image_List);
            return image_List;
        }

        /************************************************************************/
		/* 内部定義                                                             */
		/************************************************************************/

        /// <summary>
        /// リストにバインドする
        /// </summary>
        private class BindItem
        {  
            /// <summary>
            /// ファイルパス
            /// </summary>
            public string FilePath { get; set; }

            /// <summary>
            /// ファイル数
            /// </summary>
            public int FileVal { get; set; }
        };

        /************************************************************************/
        /* 変数定義                                                             */
        /************************************************************************/

        /// <summary>
        /// アイテムリスト
        /// </summary>
        List<BindItem> mItemList = new List<BindItem>();
    }
}
