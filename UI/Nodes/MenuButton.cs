﻿using Composition.Input;
using Composition.Layers;
using Composition.Nodes;
using ImageServer;
using Microsoft.Xna.Framework;
using RaceLib;
using Sound;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timing;
using Webb;
using Tools;
using UI.Video;
using Composition;
using ExternalData;

namespace UI.Nodes
{
    public class MenuButton : ImageButtonNode
    {
        private EventManager eventManager;
        private VideoManager videoManager;
        private SoundManager soundManager;
        private EventWebServer eventWebServer;
        private TimingSystemManager timingSystemManager;

        private TracksideTabbedMultiNode tabbedMultiNode;

        public event System.Action BackToEventSelector;
        public event Action<Event> Restart;
        public event System.Action ChannelsChanged;
        public event System.Action<bool> VideoSettingsExited;
        public event System.Action TimingChanged;
        public event System.Action EventChanged;
        public event System.Action DataDeleted;
        public event System.Action BugReport;

        public event System.Action OBSRemoteConfigSaved;
        public event System.Action AutoRunnerConfigsSaved;
        public event System.Action GeneralSettingsSaved;
        public event System.Action ProfileSettingsSaved;
        public event System.Action<Profile> ProfileSet;

        private Event evennt;
        private bool hasEvent;

        public Profile Profile { get; set; }

        public MenuButton(Profile profile, EventManager eventManager, VideoManager videoManager, SoundManager soundManager, EventWebServer eventWebServer, TracksideTabbedMultiNode tabbedMultiNode, Color hover, Color tint) 
            : base(@"img\settings.png", Color.Transparent, hover, tint)
        {
            Profile = profile;

            this.eventManager = eventManager;
            this.videoManager = videoManager;
            this.soundManager = soundManager;
            this.eventWebServer = eventWebServer;
            this.tabbedMultiNode = tabbedMultiNode;

            if (eventManager != null)
            {
                timingSystemManager = eventManager.RaceManager.TimingSystemManager;
                Profile = eventManager.Profile;
            }

            OnClick += SettingsButton_OnClick;
            hasEvent = true;
            ImageNode.CanScale = false;
        }

        public MenuButton(Profile profile, Color hover, Color tint) 
            : this(profile, null, null, null, null, null, hover, tint)
        {
            hasEvent = false;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (!hasEvent)
            {
                if (videoManager != null)
                    videoManager.Dispose();

                if (soundManager != null)
                    soundManager.Dispose();

                if (timingSystemManager != null)
                    timingSystemManager.Dispose();
            }
        }

        private void SettingsButton_OnClick(Composition.Input.MouseInputEvent mouseInputEvent)
        {
            bool isEmpty = true;
            bool isNotRunningRace = true;
            evennt = null;
            if (hasEvent)
            {
                evennt = eventManager.Event;
            }

            if (hasEvent)
            {
                isEmpty = eventManager.RaceManager.PilotCount == 0;
                isNotRunningRace = !eventManager.RaceManager.RaceRunning;
            }

            MouseMenu root = new MouseMenu(this);
            root.LeftToRight = false;

            if (BackToEventSelector != null)
            {
                root.AddItem("Back to Event Selection", () =>
                {
                    BackToEventSelector?.Invoke();
                });

                root.AddBlank();
            }

            root.AddItem("Online Manual", () =>
            {
                ShowOnlineManual();
            });

            if (BugReport != null)
            root.AddItem("Bug Report", () =>
            {
                BugReport();
            });

            
            if (tabbedMultiNode != null)
            {
                root.AddBlank();
                MouseMenu tabs = root.AddSubmenu("Open Tab");

                foreach (var kvp in tabbedMultiNode.Tabs)
                {
                    TextButtonNode tbn = kvp.Key;
                    Node n = kvp.Value;
                    tabs.AddItem(tbn.Text, () =>
                    {
                        tbn.ButtonNode.Click(null);
                    });
                }
            }

            if (eventWebServer != null)
            {
                MouseMenu openWebPage = root.AddSubmenu("Open Local Web page");
                foreach (string page in eventWebServer.GetPages())
                {
                    string t = page;
                    openWebPage.AddItem(t, () =>
                    {
                        OpenWebServer(t);
                    });
                }
            }
            MouseMenu openWindow = root.AddSubmenu("Open New Window");

            if (eventManager == null)
            {
                root.AddBlank();
                MouseMenu mouseMenu = root.AddSubmenu("Profile");

                mouseMenu.AddItem("Add New Profile", () =>
                {
                    TextPopupNode textPopupNode = new TextPopupNode("Add Profile", "Profile Name", "");
                    textPopupNode.OnOK += (string name) =>
                    {
                        Profile profile = Profile.AddProfile(PlatformTools.WorkingDirectory, name);

                        if (profile != null)
                        {
                            ProfileSet?.Invoke(profile);
                        }
                    };
                    GetLayer<PopupLayer>().Popup(textPopupNode);
                });
                mouseMenu.AddBlank();

                foreach (Profile profile in Profile.GetProfiles(PlatformTools.WorkingDirectory))
                {
                    Profile temp = profile;
                    mouseMenu.AddItem(profile.Name, () => { ProfileSet?.Invoke(temp); });
                }
            }

            root.AddBlank();

            root.AddItem("Auto Runner Settings", () =>
            {
                ShowAutoRunnerSettings();
            });

            root.AddItem("Channel Settings", () =>
            {
                ShowChannelSettings();
            });

            root.AddItem("Export Column Settings", () =>
            {
                ShowExportSettings();
            });

            if (hasEvent)
            {
                root.AddItem("Event Settings", () =>
                {
                    ShowEventSettings();
                });
            }

            root.AddItem("General Settings", () =>
            {
                ShowGeneralSettings();
            });

            root.AddItem("Keyboard Shortcuts", () =>
            {
                ShowKeyboardShortcuts();
            });


            root.AddItem("OBS Remote Control Settings", () =>
            {
                ShowOBSRemoteControlSettings();
            });

            root.AddItem("Profile Settings", () =>
            {
                ShowProfileSettingsEditor();
            });

            root.AddItem("Points Settings", () =>
            {
                ShowPointsSettings();

            }, isNotRunningRace);

           
            root.AddItem("Sound Editor", () =>
            {
                ShowSoundsSettings();
            });

            root.AddItem("Theme Settings", () =>
            {
                ShowThemeSettings();
            }, isNotRunningRace);


            root.AddItem("Timing Settings", () =>
            {
                ShowTimingSettings();

            }, isNotRunningRace);

            if (PlatformTools.HasFeature(PlatformFeature.Video))
            {
                root.AddItem("Video Input Settings", () =>
                {
                    ShowVideoSettings();
                });
            }

            openWindow.AddItem("Log", () =>
            {
                BaseGame baseGame = CompositorLayer.Game as BaseGame;
                baseGame.QuickLaunchWindow<LogNode>(eventManager);
            });

            if (hasEvent)
            {
                openWindow.AddItem("Rounds", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.QuickLaunchWindow<RoundsNode>(eventManager);
                });

                openWindow.AddItem("Lap Records", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.QuickLaunchWindow<LapRecordsSummaryNode>(eventManager);
                });

                openWindow.AddItem("Points", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.QuickLaunchWindow<PointsSummaryNode>(eventManager);
                });

                openWindow.AddItem("Lap Count", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.QuickLaunchWindow<LapCountSummaryNode>(eventManager);
                });

                openWindow.AddItem("Channel List", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.QuickLaunchWindow<PilotChanelList>(eventManager);
                });

                openWindow.AddItem("Event Status", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.QuickLaunchWindow<EventStatusNodeTopBar>(eventManager);
                });

                root.AddBlank();

                MouseMenu export = root.AddSubmenu("Export");
                export.AddItem("Export PBs CSV", () =>
                {
                    ExportPBsCSV();
                });

                export.AddItem("Export Raw Laps CSV", () =>
                {
                    ExportLapsCSV();
                });

                export.AddItem("Export Race Results CSV", () =>
                {
                    ExportRacesCSV();
                });


                MouseMenu delete = root.AddSubmenu("Delete from event");

                delete.AddItemConfirm("Delete all races", () =>
                {
                    DeleteAllRaceData();
                });

                delete.AddItemConfirm("Delete all pilots", () =>
                {
                    RemoveAllPilots();
                });
            }
            root.AddBlank();

            root.AddItem("Open FPVTrackside Directory", () =>
            {
                OpenDirectory();
            });

            root.AddBlank();

            root.AddItem("About", () =>
            {
                About();
            });

            root.Show(this);
        }

        public void ShowOnlineManual()
        {
            try
            {
                DataTools.StartBrowser(@"https://docs.google.com/document/d/1ysdQD3JdPvdTsNZR1Q_jh6voXrC2lJlIUAcL1TOb670");
            }
            catch
            {
            }
        }

        public void ShowChannelSettings()
        {
            Channel[] channels;
            if (eventManager == null)
            {
                channels = Channel.Read(Profile);
            }
            else
            {
                channels = eventManager.Channels;
            }

            ChannelEditor editor = new ChannelEditor(channels, eventManager != null);
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                channels = editor.Objects.OrderBy(r => r.Frequency).ThenBy(r => r.Band).ToArray();

                if (editor.SaveDefault)
                {
                    Channel.Write(Profile, channels);
                }

                if (eventManager != null)
                {
                    using (Database db = new Database())
                    {
                        eventManager.Event.Channels = channels;
                        db.Events.Update(eventManager.Event);
                    }
                    eventManager.SetChannelColors(Theme.Current.ChannelColors.XNA());
                }

                ChannelsChanged?.Invoke();
            };
        }

        public void ShowVideoSettings()
        {
            videoManager?.StopDevices();

            VideoSourceEditor editor = VideoSourceEditor.GetVideoSourceEditor(eventManager, Profile);
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                List<VideoConfig> sources = editor.Objects.ToList();
                VideoManager.WriteDeviceConfig(Profile, sources);

                VideoSettingsExited?.Invoke(true);
            };

            editor.OnCancel += (e) =>
            {
                VideoSettingsExited?.Invoke(false);
            };
        }

        public void ShowPointsSettings()
        {
            PointsSettings settings = null;

            if (eventManager != null)
            {
                settings = eventManager.ResultManager.PointsSettings;
            }
            else
            {
                settings = PointsSettings.Read(Profile);
            }

            ObjectEditorNode<PointsSettings> editor = new ObjectEditorNode<PointsSettings>(settings, false, true, false);
            editor.OnOK += (a) =>
            {
                settings = a.Objects.FirstOrDefault();
                PointsSettings.Write(Profile, settings);
                if (eventManager != null)
                {
                    eventManager.ResultManager.PointsSettings = settings;
                }
            };
            GetLayer<PopupLayer>().Popup(editor);
        }

        public void ShowTimingSettings()
        {
            if (timingSystemManager == null)
            {
                timingSystemManager = new TimingSystemManager(Profile);
            }

            TimingSystemSettings[] timingSystemSettings = TimingSystemSettings.Read(Profile);

            TimingSystemEditor editor = new TimingSystemEditor(timingSystemSettings);
            editor.OnOK += (a) =>
            {
                timingSystemManager.TimingSystemsSettings = a.Objects.ToArray();
                TimingSystemSettings.Write(Profile, timingSystemManager.TimingSystemsSettings);
                timingSystemManager.InitialiseTimingSystems(Profile);

                TimingChanged?.Invoke();
            };
            GetLayer<PopupLayer>().Popup(editor);
        }

        public void ShowSoundsSettings()
        {
            if (soundManager == null)
            {
                ProfileSettings profileSettings = ProfileSettings.Read(Profile);

                soundManager = new SoundManager(null, Profile);
                soundManager.SetupSpeaker(PlatformTools, profileSettings.Voice, profileSettings.TextToSpeechVolume);
                soundManager.WaitForInit();
            }

            SoundEditor soundEditor = new SoundEditor(soundManager);
            soundEditor.OnOK += (a) =>
            {
                if (soundManager != null)
                {
                    soundManager.WriteSettings();
                }
            };

            GetLayer<PopupLayer>().Popup(soundEditor);
        }

        public void ShowExportSettings()
        {
            GetLayer<PopupLayer>().Popup(new ExportColumnEditor(eventManager, Profile));
        }

        public void ShowOBSRemoteControlSettings()
        {
            OBSRemoteControlManager.OBSRemoteControlConfig config = OBSRemoteControlManager.OBSRemoteControlConfig.Load(Profile);

            var editor = new OBSRemoteControlEditor(config);
            editor.OnOK += (e) =>
            {
                config.RemoteControlEvents = editor.Objects.ToList();
                OBSRemoteControlManager.OBSRemoteControlConfig.Write(Profile, config);

                OBSRemoteConfigSaved?.Invoke();
            };

            GetLayer<PopupLayer>().Popup(editor);
        }
        public void ShowAutoRunnerSettings()
        {
            AutoRunnerConfig config = AutoRunnerConfig.Load(Profile);

            AutoRunnerConfigEditor editor = new AutoRunnerConfigEditor(config);
            editor.OnOK += (e) =>
            {
                AutoRunnerConfig.Write(Profile, config);
                AutoRunnerConfigsSaved?.Invoke();
            };

            GetLayer<PopupLayer>().Popup(editor);
        }

        public void ShowEventSettings()
        {
            EventEditor editor = new EventEditor(eventManager.Event);
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                eventManager.Event = editor.Objects.FirstOrDefault();
                using (Database db = new Database())
                {
                    db.Events.Update(eventManager.Event);
                }

                EventChanged?.Invoke();
            };
        }

        public void ShowGeneralSettings()
        {
            GeneralSettingsEditor editor = new GeneralSettingsEditor(GeneralSettings.Instance);
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                GeneralSettings.Write();
                if (hasEvent && Restart != null && editor.NeedsRestart)
                {
                    GetLayer<PopupLayer>().PopupConfirmation("Changes require restart to take effect. Restart now?", () => { Restart(evennt); });
                }

                GeneralSettingsSaved?.Invoke();
            };
        }

        public void ShowProfileSettingsEditor()
        {
            ProfileSettings profileSettings = ProfileSettings.Read(Profile);

            ProfileSettingsEditor editor = new ProfileSettingsEditor(profileSettings);
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                ProfileSettings.Write(Profile, profileSettings);
                if (hasEvent && Restart != null && editor.NeedsRestart)
                {
                    GetLayer<PopupLayer>().PopupConfirmation("Changes require restart to take effect. Restart now?", () => { Restart(evennt); });
                }

                ProfileSettingsSaved?.Invoke();
            };
        }

        public void ShowThemeSettings()
        {
            ThemeSettingsEditor editor = new ThemeSettingsEditor(Profile, Theme.Themes);
            editor.Selected = Theme.Current;
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                Restart?.Invoke(evennt);
            };
        }

        public void ShowKeyboardShortcuts()
        {
            var k = KeyboardShortcuts.Read(Profile);

            KeyboardShortcutsEditor editor = new KeyboardShortcutsEditor(k);
            editor.Selected = k;
            GetLayer<PopupLayer>().Popup(editor);

            editor.OnOK += (e) =>
            {
                KeyboardShortcuts.Write(Profile, k);
                Restart?.Invoke(evennt);
            };
        }

        public void About()
        {
            AboutNode aboutNode = new AboutNode();
            GetLayer<PopupLayer>().Popup(aboutNode);
            RequestLayout();
        }

        public void OpenDirectory()
        {
            PlatformTools.OpenFileManager(System.IO.Directory.GetCurrentDirectory());
        }

        public void OpenWebServer(string page)
        {
            if (eventWebServer != null)
            {
                if (GeneralSettings.Instance.HTTPServer == false)
                {
                    GeneralSettings.Instance.HTTPServer = true;
                }

                if (!eventWebServer.Running)
                {
                    eventWebServer.Start();
                }

                DataTools.StartBrowser(eventWebServer.Url + page);
            }
        }

        public void ExportPBsCSV()
        {
            PlatformTools.ExportCSV("Save Top Consecutive Laps CSV", eventManager.LapRecordManager.ExportPBsCSV(), GetLayer<PopupLayer>());
        }

        public void ExportRacesCSV()
        {
            PlatformTools.ExportCSV("Save Race Results CSV", eventManager.RaceManager.GetRaceResultsText(","), GetLayer<PopupLayer>());
        }

        public void ExportLapsCSV()
        {
            PlatformTools.ExportCSV("Save Top Consecutive Laps CSV", eventManager.RaceManager.GetRawLaps(), GetLayer<PopupLayer>());
        }

        public void DeleteAllRaceData()
        {
            eventManager.RaceManager.DeleteRaces();
            eventManager.LapRecordManager.Clear();

            DataDeleted?.Invoke();
        }

        public void RemoveAllPilots()
        {
            eventManager.RemovePilots();

            DataDeleted?.Invoke();
        }

    }
}
