using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace InstaTech_Player
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string[] FileArray { get; set; }
        long PlayPosition { get; set; } = 0;
        FileStream FS { get; set; }
        StreamReader SR { get; set; }
        bool IsPlaying { get; set; } = false;
        int PlaySpeed { get; set; } = 1;
        bool QuickSeekEnabled { get; set; } = false;
        long SeekTo { get; set; } = 0;
        Bitmap SourceImage { get; set; }
        Graphics Graphic { get; set; }
        string[] CurrentFrame { get; set; }
        string[] NextFrame { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            WPF_Auto_Update.Updater.ServiceURI = "http://instatech.invis.me/Services/Get_Player_Version.cshtml";
            WPF_Auto_Update.Updater.RemoteFileURI = "http://instatech.invis.me/Downloads/InstaTech_Player.exe";
            WPF_Auto_Update.Updater.CheckCommandLineArgs();
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await WPF_Auto_Update.Updater.CheckForUpdates(true);
        }
        private void buttonMenu_Click(object sender, RoutedEventArgs e)
        {
            buttonMenu.ContextMenu.IsOpen = !buttonMenu.ContextMenu.IsOpen;
        }

        private void menuOpen_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.CheckFileExists = true;
            ofd.DefaultExt = ".itr";
            ofd.Filter = "InstaTech Recordings (*.itr) | *.itr";
            ofd.Multiselect = false;
            ofd.Title = "Open InstaTech Recording";
            ofd.ShowDialog();
            if (File.Exists(ofd.FileName))
            {
                textDropHere.Visibility = Visibility.Collapsed;
                LoadFile(ofd.FileName);
            }
            
        }

        private void menuQuickSeek_Checked(object sender, RoutedEventArgs e)
        {
            QuickSeekEnabled = true;
        }

        private void menuQuickSeek_Unchecked(object sender, RoutedEventArgs e)
        {
            QuickSeekEnabled = false;
        }
        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] data = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (data.Length > 1)
            {
                MessageBox.Show("You may only open one file at a time.", "One File Only", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (Directory.Exists(data[0]))
            {
                MessageBox.Show("You cannot open directories.", "File Only", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (System.IO.Path.GetExtension(data[0]) != ".itr")
            {
                MessageBox.Show("You can only open ITR files.", "ITR Files Only", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            textDropHere.Visibility = Visibility.Collapsed;
            LoadFile(data[0]);
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (IsPlaying)
            {
                return;
            }
            IsPlaying = true;
            PlayFile();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            IsPlaying = false;
        }

        private void sliderVideo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsPlaying)
            {
                if (SR != null)
                {
                    if (!QuickSeekEnabled)
                    {
                        SeekTo = (long)sliderVideo.Value;
                        FS.Position = 0;
                    }
                    else
                    {
                        SR.BaseStream.Position = (long)sliderVideo.Value;
                        Graphic.Clear(Color.Transparent);
                    }
                }
            }
        }

        private void sliderVideo_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            IsPlaying = false;
        }

        private void buttonPlaySpeedUp_Click(object sender, RoutedEventArgs e)
        {
            PlaySpeed++;
            textPlaySpeed.Text = $"x{PlaySpeed}";
        }

        private void buttonPlaySpeedDown_Click(object sender, RoutedEventArgs e)
        {
            if (PlaySpeed > 1)
            {
                PlaySpeed--;
                textPlaySpeed.Text = $"x{PlaySpeed}";
            }
        }

        private void LoadFile(string FilePath)
        {
            try
            {
                if (SR != null)
                {
                    SR.Close();
                }
                if (FS != null)
                {
                    FS.Close();
                }
                FS = new FileStream(FilePath, FileMode.Open);
                SR = new StreamReader(FS);
                sliderVideo.Maximum = FS.Length;
                PlayFile();
            }
            catch
            {
                MessageBox.Show("There was an error loading the file.", "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        private async void PlayFile()
        {
            if (SR == null)
            {
                return;
            }
            IsPlaying = true;
            while (IsPlaying && !SR.EndOfStream)
            {
                try
                {
                    if (CurrentFrame == null)
                    {
                        CurrentFrame = (await SR.ReadLineAsync()).Split(',');
                    }
                    if (DateTime.TryParse(CurrentFrame[0], out DateTime timeStamp))
                    {
                        textTimestamp.Text = timeStamp.ToString();
                        textTimestamp.UpdateLayout();
                        var imageData = CurrentFrame[1];
                        var byteArray = Convert.FromBase64String(imageData);
                        var length = byteArray.Length;

                        // Get the XY coordinate of the top-left of the image based on the last 6 bytes appended to the array.
                        var imgX = (byteArray[length - 6] * 10000) + (byteArray[length - 5] * 100) + byteArray[length - 4];
                        var imgY = (byteArray[length - 3] * 10000) + (byteArray[length - 2] * 100) + byteArray[length - 1];
                        using (var ms = new MemoryStream(byteArray.Take(length - 6).ToArray()))
                        {
                            using (var tempImage = new Bitmap(ms))
                            {
                                Graphic.DrawImage(tempImage, imgX, imgY);
                            }
                        }
                        picturePlayer.Refresh();
                        NextFrame = SR.ReadLine()?.Split(',');
                        if (NextFrame == null)
                        {
                            continue;
                        }
                        DateTime nextTimeStamp;
                        while (!DateTime.TryParse(NextFrame?[0], out nextTimeStamp) && !SR.EndOfStream)
                        {
                            if (int.TryParse(NextFrame[0], out int width) && int.TryParse(NextFrame[1], out int height))
                            {
                                hostPlayer.Width = width;
                                hostPlayer.Height = height;
                                SourceImage = new Bitmap(width, height);
                                Graphic = Graphics.FromImage(SourceImage);
                                picturePlayer.Image = SourceImage;
                            }
                            NextFrame = SR.ReadLine().Split(',');
                        }
                        if (FS.Position < SeekTo && !QuickSeekEnabled)
                        {
                            CurrentFrame = NextFrame;
                            continue;
                        }
                        if (nextTimeStamp > timeStamp)
                        {
                            await Task.Delay((nextTimeStamp - timeStamp).Milliseconds / PlaySpeed);
                        }
                        else
                        {
                            await Task.Delay(1);
                        }
                    }
                    else
                    {
                        if (int.TryParse(CurrentFrame[0], out int width) && int.TryParse(CurrentFrame[1], out int height))
                        {
                            hostPlayer.Width = width;
                            hostPlayer.Height = height;
                            SourceImage = new Bitmap(width, height);
                            Graphic = Graphics.FromImage(SourceImage);
                            picturePlayer.Image = SourceImage;
                        }
                    }
                }
                catch
                {
                    NextFrame = SR.ReadLine()?.Split(',');
                }
                if (IsPlaying)
                {
                    CurrentFrame = NextFrame;
                    sliderVideo.Value = FS.Position;
                }
            }
            IsPlaying = false;
        }

    }
}
