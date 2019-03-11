﻿using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

using HearMe.Models;
using HearMe.Helpers;
using CSCore;
using System.Linq;
using PlaylistsNET.Models;
using GalaSoft.MvvmLight.CommandWpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Net;

namespace HearMe.ViewModels
{
    class PlayerViewModel : GalaSoft.MvvmLight.ObservableObject, IDisposable
    {
        public PlayerViewModel()
        {
            NavigationRequest += HandleNavigationRequest;

            DataUpdateRequest += UpdatePlayingSongPosition;

            // Disabled for now as it is not displayed
            // DataUpdateRequest += UpdatePlayingSongDisplayText;

            Playlist = new Playlist();

            SetupKeyboardHooks();

            NextCommand = new RelayCommand(() => MovePlaylistSong(1));
            PreviousCommand = new RelayCommand(() => MovePlaylistSong(-1));
            StopCommand = new RelayCommand(() => Stop());
            PlayCommand = new RelayCommand(() => TogglePlay());
            OpenPlaylistCommand = new RelayCommand(() => Playlist.Open());
            SavePlaylistCommand = new RelayCommand(() => Playlist.Save());
            ClearPlaylistCommand = new RelayCommand(() => ClearPlaylist());
            DropCommand = new RelayCommand<DragEventArgs>((e) => AddToPlaylist(e), (e) => true);
            DeleteSelectedCommand = new RelayCommand<KeyEventArgs>((e) => DeleteSelectedFile(e), (e) => true);

            _timer = new System.Timers.Timer
            {
                Interval = 300
            };

            _timer.Elapsed += UpdateBoundData;
        }

        private string _playButtonIcon;
        public string PlayButtonIcon
        {
            get { return _playButtonIcon; }
            set
            {
                _playButtonIcon = value;
                RaisePropertyChanged("PlayButtonIcon");
            }
        }

        private string _playingsongTitle;
        public string PlayingSongTitle
        {
            get { return _playingsongTitle; }
            set
            {
                _playingsongTitle = value;
                RaisePropertyChanged("PlayingSongTitle");
            }
        }

        private string _playingsongArtist;
        public string PlayingSongArtist
        {
            get { return _playingsongArtist; }
            set
            {
                _playingsongArtist = value;
                RaisePropertyChanged("PlayingSongArtist");
            }
        }

        private BitmapImage _albumArt;
        public BitmapImage AlbumArt
        {
            get { return _albumArt; }
            set
            {
                _albumArt = value;
                RaisePropertyChanged("AlbumArt");
            }
        }

        private Double _playingsongLength;
        public Double PlayingSongLength
        {
            get { return _playingsongLength; }
            set
            {
                _playingsongLength = value;
                RaisePropertyChanged("PlayingSongLength");
            }
        }

        private double _playingsongPosition;
        public double PlayingSongPosition
        {
            get { return _playingsongPosition; }
            set
            {
                SetPosition(value);
            }
        }

        private float _volume;
        public float Volume
        {
            get { return _volume; }
            set
            {
                if (_volume != value)
                {
                    SetVolume(value);
                    _volume = value;
                    RaisePropertyChanged("Volume");
                }
            }
        }

        public TimeSpan CurrentTime
        {
            get { return (audioPlayer != null) ? audioPlayer.AudioFile.GetPosition() : TimeSpan.Zero; }
        }

        public TimeSpan TotalTime
        {
            get { return (audioPlayer != null) ? audioPlayer.AudioFile.GetLength() : TimeSpan.Zero; }
        }

        private string _displayText;
        public string DisplayText
        {
            get { return _displayText; }
            set
            {
                _displayText = value;
                RaisePropertyChanged("DisplayText");
            }
        }

        private int PlayingSongPlaylistIndex { get; set; }
        public Playlist Playlist { get; set; }

        public RelayCommand NextCommand { get; private set; }
        public RelayCommand PreviousCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }
        public RelayCommand PlayCommand { get; private set; }
        public RelayCommand OpenPlaylistCommand { get; private set; }
        public RelayCommand SavePlaylistCommand { get; private set; }
        public RelayCommand ClearPlaylistCommand { get; private set; }
        public RelayCommand<DragEventArgs> DropCommand { get; private set; }
        public RelayCommand<KeyEventArgs> DeleteSelectedCommand { get; private set; }

        AudioPlayer audioPlayer;
        private GlobalKeyboardHook _globalKeyboardHook;
        private System.Timers.Timer _timer;

        public event EventHandler<NavigationEventArgs> NavigationRequest;
        public event EventHandler<EventArgs> DataUpdateRequest;

        private void TogglePlay()
        {
            if (audioPlayer == null)
                return;

            if (audioPlayer.IsPlaying())
            {
                Stop();
            }
            else
            {
                Play();
            }
        }

        private void DeleteSelectedFile(KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                ListBox playlistListBox = (ListBox)e.Source;
                Playlist.Remove(playlistListBox.SelectedItems);
            }
        }

        public void AddToPlaylist(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Playlist.Add((string[])e.Data.GetData(DataFormats.FileDrop));
            }
        }

        void ClearPlaylist()
        {
            Playlist.Clear();
            PlayingSongPlaylistIndex = -1;
        }

        public void UnbindOnStopEvent()
        {
            if (audioPlayer != null)
                audioPlayer.OutputDevice.Stopped -= PlaybackDevicePlaybackStopped;
        }

        public void PlayFile(int playlistIndex)
        {
            M3uPlaylistEntry selectedSong = Playlist.ElementAt(playlistIndex);

            if (selectedSong == null)
            {
                return;
            }

            string fileLocation = selectedSong.Path;

            if (!System.IO.File.Exists(fileLocation))
            {
                return;
            }

            if (audioPlayer != null)
            {
                Dispose();
            }

            PlayingSongPlaylistIndex = playlistIndex;

            UpdateSongInformationDisplay(selectedSong);

            audioPlayer = new AudioPlayer(@fileLocation);
            audioPlayer.Play();

            PlayButtonIcon = WebUtility.HtmlDecode("&#9724;");

            audioPlayer.OutputDevice.Stopped += PlaybackDevicePlaybackStopped;

            PlayingSongLength = TotalTime.TotalSeconds;

            audioPlayer.OutputDevice.Volume = Volume;

            _timer.Start();
            
        }

        public void UpdateBoundData(object sender, System.Timers.ElapsedEventArgs e)
        {
            DataUpdateRequest.Invoke(this, null);
        }

        public void UpdatePlayingSongPosition(object sender, EventArgs e)
        {
            _playingsongPosition = CurrentTime.TotalSeconds;
            RaisePropertyChanged("PlayingSongPosition");
        }

        private void UpdatePlayingSongDisplayText(object sender, EventArgs e)
        {
            DisplayText = CurrentTime.ToString("mm\\:ss") + " / " + TotalTime.ToString("mm\\:ss");
        }

        public void HandleNavigationRequest(object sender, NavigationEventArgs e)
        {
            // Don't allow movement beyond last playlist song
            if (PlayingSongPlaylistIndex + e.Direction == Playlist.Files.Count)
            {
                return;
            }

            // Don't allow movement below 0
            if (PlayingSongPlaylistIndex + e.Direction < 0)
            {
                return;
            }

            PlayingSongPlaylistIndex += e.Direction;

            PlayFile(PlayingSongPlaylistIndex);
        }

        public void MovePlaylistSong(sbyte indexMovementDirection)
        {
            NavigationRequest.Invoke(this, new NavigationEventArgs {Direction = indexMovementDirection });
        }

        void PlaybackDevicePlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (audioPlayer.OutputDevice.PlaybackState == CSCore.SoundOut.PlaybackState.Playing)
            {
                return;
            }

            if (audioPlayer.IsPlaying())
            {
                MovePlaylistSong(1);
                return;
            }
        }

        public void Play()
        {
            if (audioPlayer == null)
            {
                return;
            }

            audioPlayer.OutputDevice.Volume = Volume;
            audioPlayer.Play();
            PlayButtonIcon = WebUtility.HtmlDecode("&#9724;"); 
            _timer.Start();
        }

        public void Stop()
        {
            if (audioPlayer != null)
            {
                audioPlayer.Stop();
                PlayButtonIcon = WebUtility.HtmlDecode("&#10095;");
                _timer.Stop();
            }
        }

        public void SetVolume(float volumeLevel)
        {
            if (audioPlayer != null)
                audioPlayer.OutputDevice.Volume = Volume;
        }

        public void SetPosition(double newPosition)
        {
            TimeSpan seekPosition = new TimeSpan(0, (int)(Math.Floor(newPosition / 60)), (int)(Math.Floor(newPosition % 60)));
            audioPlayer.AudioFile.SetPosition(seekPosition);
        }

        public void UpdateSongInformationDisplay(M3uPlaylistEntry playingSong)
        {
            AlbumArt = Song.GetAlbumArt(playingSong.Path);
            PlayingSongArtist = playingSong.AlbumArtist;
            PlayingSongTitle = playingSong.Title;
        }

        public void Dispose()
        {
            if (audioPlayer == null)
            {
                return;
            }

            if (audioPlayer.OutputDevice != null)
            {
                audioPlayer.OutputDevice.Dispose();
                audioPlayer.OutputDevice = null;
            }

            if (audioPlayer.AudioFile != null)
            {
                audioPlayer.AudioFile.Dispose();
                audioPlayer.AudioFile = null;
            }

            audioPlayer = null;
        }


        public void SetupKeyboardHooks()
        {
            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyboardPressed += OnKeyPressed;
        }

        private void OnKeyPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
           if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown)
            {

                int[] validKeys = new int[] {GlobalKeyboardHook.VkMediaNext, GlobalKeyboardHook.VkMediaPrevious, GlobalKeyboardHook.VkMediaPlay};

                if (!validKeys.Contains(e.KeyboardData.VirtualCode))
                {
                    return;
                }

                if (e.KeyboardData.VirtualCode == GlobalKeyboardHook.VkMediaNext)
                    MovePlaylistSong(1);

                if (e.KeyboardData.VirtualCode == GlobalKeyboardHook.VkMediaPrevious)
                    MovePlaylistSong(-1);

                if (e.KeyboardData.VirtualCode == GlobalKeyboardHook.VkMediaPlay)
                {
                    if (audioPlayer == null)
                        return;

                    if (audioPlayer.IsPlaying())
                    {
                        Stop();
                    }
                    else
                    {
                        Play();
                    }
                }

                e.Handled = true;
            }
        }
    }
}
