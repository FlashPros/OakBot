using System;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.ComponentModel;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Threading;

using OakBot.Model;

namespace OakBot.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        #region Fields

        // Services
        private readonly IChatConnectionService _ccs;
        private readonly IWebSocketEventService _wse;
        private readonly IBinFileService _bfh;
        private readonly IAuthenticationService _auths;

        private MainSettings _mainSettings;

        private TwitchCredentials _botCredentials;
        private TwitchCredentials _casterCredentials;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(IChatConnectionService ccs, IBinFileService bfh,
            IWebSocketEventService wse, IAuthenticationService auths)
        {          
            Title = "OakBot - Giveaway Bot";

            // Set dependency injection references
            _ccs = ccs;
            _wse = wse;
            _bfh = bfh;
            _auths = auths;

            // Initialize collections
            _chatAccounts = new ObservableCollection<ITwitchAccount>();
            _chatmessages = new LimitedObservableCollection<TwitchChatMessage>(500);

            // Register for chat connection service events
            _ccs.ChatMessageReceived += _ccs_MessageReceived;
            _ccs.Connected += _ccs_Connected;
            _ccs.Authenticated += _ccs_Authenticated;
            _ccs.Disconnected += _ccs_Disconnected;

            _wse.SetApiToken("oakbotapitest");

            // Load settings if available
            var loadedSettings = (MainSettings)_bfh.ReadBinFile("LoginSettings");
            if (loadedSettings == null)
            {
                // Settings file does not exist yet
                _mainSettings = new MainSettings();
            }
            else
            {
                // Load settings to properties to trigger UI update
                _mainSettings = loadedSettings;
                ChannelName = _mainSettings.Channel;
                BotUsername = _mainSettings.BotUsername;
                CasterUsername = _mainSettings.CasterUsername;

                // Set bot credentials
                if (!string.IsNullOrWhiteSpace(_mainSettings.BotOauthKey))
                {
                    IsBotOauthSet = true;
                    _botCredentials = new TwitchCredentials(
                        _mainSettings.BotUsername, _mainSettings.BotOauthKey, false);
                }

                // Set caster credentials
                if (!string.IsNullOrWhiteSpace(_mainSettings.CasterOauthKey))
                {
                    IsCasterOauthSet = true;
                    _casterCredentials = new TwitchCredentials(
                        _mainSettings.CasterUsername, _mainSettings.CasterOauthKey, true);
                }
            }
            
        }

        #endregion

        #region Private Methods

        private void OnSettingsChanged()
        {
            _bfh.WriteBinFile("LoginSettings", _mainSettings);
            _ccs.SetJoiningChannel(_mainSettings.Channel, false);
        }

        private void AddChatMessage(TwitchChatMessage tcm)
        {
            // Add message on via the main thread
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                _chatmessages.AddAndTrim(tcm);
            });
        }

        private void ConnectDisconnectChat(bool isCaster)
        {
            if (isCaster)
            {
                if (IsCasterConnected)
                {
                    _ccs.Disconnect(true);
                }
                else
                {
                    // Credentials should be present
                    if (_casterCredentials != null)
                    {
                        // Set credentials
                        _ccs.SetCredentials(_casterCredentials);

                        // Connect
                        _ccs.Connect(true);
                    }
                }
            }
            else
            {
                if (IsBotConnected)
                {
                    if (IsCasterConnected)
                    {
                        ConnectDisconnectChat(true);
                    }

                    _ccs.Disconnect(false);
                }
                else
                {
                    // Credentials should be present
                    if (_botCredentials != null)
                    {
                        // Set channel
                        _ccs.SetJoiningChannel(ChannelName, true);

                        // Set credentials
                        _ccs.SetCredentials(_botCredentials);

                        // Connect
                        _ccs.Connect(false);
                    }
                }
            }
        }

        private bool CanConnectDisconnectExecute(bool isCaster)
        {
            // Return false if channel name is not valid
            if (!Regex.IsMatch(ChannelName ?? string.Empty, @"^[a-z0-9][a-z0-9_]{3,24}$"))
                return false;

            if (isCaster)
            {
                // Return false if caster oauth is not set
                if (!IsCasterOauthSet)
                    return false;

                // Return result for valid given caster username
                return Regex.IsMatch(CasterUsername ?? string.Empty, @"^[a-z0-9][a-z0-9_]{3,24}$");
            }
            else
            {
                // Return false if bot oauth is not set
                if (!IsBotOauthSet)
                    return false;

                // Return false for valid given bot username
                return Regex.IsMatch(BotUsername ?? string.Empty, @"^[a-z0-9][a-z0-9_]{3,24}$");
            }
        }
        
        #endregion

        #region Event Handlers

        private void _ccs_MessageReceived(object sender, ChatConnectionMessageReceivedEventArgs e)
        {
            // Add received message to collection
            AddChatMessage(e.ChatMessage);

            // Broadcast received message for other VM
            Messenger.Default.Send<TwitchChatMessage>(e.ChatMessage);
        }

        private void _ccs_Connected(object sender, ChatConnectionConnectedEventArgs e)
        {
            AddChatMessage(new TwitchChatMessage(
                    string.Format("{0} is successfully connected to chat server. Authenticating now.", e.Account.Username),
                    "oakbot",
                    "OakBot")
            );
        }

        private void _ccs_Authenticated(object sender, ChatConnectionAuthenticatedEventArgs e)
        {
            if (e.IsAuthenticated)
            {
                // Set connected state for UI
                if (e.Account.IsCaster)
                {
                    IsCasterConnected = true;
                }
                else
                {
                    IsBotConnected = true;
                    _wse.BroadcastEvent("OAKBOT_CHAT_CONNECTED", new { name = e.Account.Username });
                }

                // Add account to collection
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    // Add
                    _chatAccounts.Add(e.Account);

                    // If only account, pre-select it
                    if (_chatAccounts.Count == 1)
                    {
                        SelectedAccount = e.Account;
                    }
                });

                // Add success message in console
                AddChatMessage(new TwitchChatMessage(
                    string.Format("{0} successfully authenticated, have fun!", e.Account.Username),
                    "oakbot", "Oakbot")
                );
            }
            else
            {
                // Add failure message in console
                AddChatMessage(new TwitchChatMessage(
                    string.Format("{0} failed to authenticate, please relink with Twitch!", e.Account.Username),
                    "oakbot", "Oakbot")
                );
            }
        }

        private void _ccs_Disconnected(object sender, ChatConnectionDisconnectedEventArgs e)
        {
            // Set flag for UI notification
            if (e.Account.IsCaster)
            {
                IsCasterConnected = false;
            }
            else
            {
                IsBotConnected = false;
                _wse.BroadcastEvent("OAKBOT_CHAT_DISCONNECTED", new { name = e.Account.Username });
            }

            // Remove account from chat accounts collection
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // Remove
                _chatAccounts.Remove(e.Account);

                // Selected account got disconnected
                if (SelectedAccount == null && _chatAccounts.Count != 0)
                {
                    SelectedAccount = _chatAccounts[0];
                }
            });

            // Add disconnection message to console
            AddChatMessage(new TwitchChatMessage(
                string.Format("{0} disconnected from the chat.", e.Account.Username),
                "oakbot", "OakBot"));
        }

        #endregion

        #region General Properties

        public string Title { get; private set; }

        private ICommand _cmdOnClose;
        public ICommand CmdOnClose
        {
            get
            {
                return _cmdOnClose ??
                    (_cmdOnClose = new RelayCommand<CancelEventArgs>(
                        args =>
                        {
                            var res = MessageBox.Show("Are you sure to shutdown OakBot?",
                                "OakBot - Shutdown Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                if (IsCasterConnected)
                                    _ccs.Disconnect(true);

                                if (IsBotConnected)
                                    _ccs.Disconnect(false);

                                // Continue shutting down
                                args.Cancel = false;
                            }
                            else
                            {
                                // Cancel shutting down
                                args.Cancel = true;
                            }
                        }
                    ));
            }
        }

        #endregion

        #region Authentication and Chat Connect Properties

        private string _channelName;
        public string ChannelName
        {
            get
            {
                return _channelName;
            }
            set
            {
                var lowered = value.ToLower();
                if (lowered != _channelName)
                {
                    _channelName = lowered;
                    RaisePropertyChanged();

                    _mainSettings.Channel = lowered;
                    OnSettingsChanged();
                }
            }
        }

        private string _botUsername;
        public string BotUsername
        {
            get
            {
                return _botUsername;
            }
            set
            {
                var lowered = value.ToLower();
                if (lowered != _botUsername)
                {
                    _botUsername = lowered;
                    RaisePropertyChanged();

                    _mainSettings.BotUsername = lowered;
                    OnSettingsChanged();
                }
            }
        }

        private bool _isBotOauthSet = false;
        public bool IsBotOauthSet
        {
            get
            {
                return _isBotOauthSet;
            }
            private set
            {
                _isBotOauthSet = value;
                RaisePropertyChanged();
            }
        }

        private ICommand _cmdAuthBot;
        public ICommand CmdAuthBot
        {
            get
            {
                return _cmdAuthBot ??
                    (_cmdAuthBot = new RelayCommand(
                        () =>
                        {
                            string res = _auths.AuthenticateTwitch(BotUsername, true);
                            if (res != null)
                            {
                                _mainSettings.BotOauthKey = res;
                                OnSettingsChanged();

                                IsBotOauthSet = true;
                                _botCredentials = new TwitchCredentials(
                                    _mainSettings.BotUsername, _mainSettings.BotOauthKey, false);
                            }
                        },
                        () =>
                        {
                            return (!string.IsNullOrWhiteSpace(BotUsername) &&
                                Regex.IsMatch(BotUsername, @"^[a-z0-9][a-z0-9_]{3,24}$"));
                        }
                    ));
            }
        }

        private string _casterUsername;
        public string CasterUsername
        {
            get
            {
                return _casterUsername;
            }
            set
            {
                var lowered = value.ToLower();
                if (lowered != _casterUsername)
                {
                    _casterUsername = lowered;
                    RaisePropertyChanged();

                    _mainSettings.CasterUsername = lowered;
                    OnSettingsChanged();
                }
            }
        }

        private bool _isCasterOauthSet = false;
        public bool IsCasterOauthSet
        {
            get
            {
                return _isCasterOauthSet;
            }
            private set
            {
                _isCasterOauthSet = value;
                RaisePropertyChanged();
            }
        }

        private ICommand _cmdAuthCaster;
        public ICommand CmdAuthCaster
        {
            get
            {
                return _cmdAuthCaster ??
                    (_cmdAuthCaster = new RelayCommand(
                        () =>
                        {
                            string res = _auths.AuthenticateTwitch(CasterUsername, false);
                            if (res != null)
                            {
                                _mainSettings.CasterOauthKey = res;
                                OnSettingsChanged();

                                IsCasterOauthSet = true;
                                _casterCredentials = new TwitchCredentials(
                                    _mainSettings.CasterUsername, _mainSettings.CasterOauthKey, true);
                            }
                        },
                        () =>
                        {
                            return (!string.IsNullOrWhiteSpace(CasterUsername) &&
                                Regex.IsMatch(CasterUsername, @"^[a-z0-9][a-z0-9_]{3,24}$"));
                        }
                    ));
            }
        }

        private bool _isBotConnected;
        public bool IsBotConnected
        {
            get
            {
                return _isBotConnected;
            }
            private set
            {
                if (value != _isBotConnected)
                {
                    _isBotConnected = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ICommand _cmdConnectBot;
        public ICommand CmdConnectBot
        {
            get
            {
                return _cmdConnectBot ?? (_cmdConnectBot = new RelayCommand(
                        () => ConnectDisconnectChat(false),
                        () => CanConnectDisconnectExecute(false)
                    ));
            }
        }

        private bool _isCasterConnected;
        public bool IsCasterConnected
        {
            get
            {
                return _isCasterConnected;
            }
            private set
            {
                if (value != _isCasterConnected)
                {
                    _isCasterConnected = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ICommand _cmdConnectCaster;
        public ICommand CmdConnectCaster
        {
            get
            {
                return _cmdConnectCaster ?? (_cmdConnectCaster = new RelayCommand(
                        () => ConnectDisconnectChat(true),
                        () => CanConnectDisconnectExecute(true)
                    ));
            }
        }

        #endregion

        #region Console Properties

        /// <summary>
        /// The <see cref="TwitchChatMessage"/> collection for bindings.
        /// </summary>
        private LimitedObservableCollection<TwitchChatMessage> _chatmessages;
        public LimitedObservableCollection<TwitchChatMessage> ChatMessages
        {
            get
            {
                return _chatmessages;
            }
        }

        /// <summary>
        /// The connected <see cref="ITwitchAccount"/> chat accounts collection for bindings.
        /// </summary>
        private ObservableCollection<ITwitchAccount> _chatAccounts;
        public ObservableCollection<ITwitchAccount> ChatAccounts
        {
            get
            {
                return _chatAccounts;
            }
        }

        /// <summary>
        /// The selected account to send chat message as for bindings.
        /// </summary>
        private ITwitchAccount _selectedAccount;
        public ITwitchAccount SelectedAccount
        {
            get
            {
                return _selectedAccount;
            }
            set
            {
                if (value != _selectedAccount)
                {
                    _selectedAccount = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Message to send to chat for bindings.
        /// </summary>
        private string _messageToSend;
        public string MessageToSend
        {
            get
            {
                return _messageToSend;
            }
            private set
            {
                // dont compare for change, as its private set, ui cant change
                _messageToSend = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// UI Command binding to send given message to with selected account.
        /// </summary>
        private ICommand _cmdSendMessage;
        public ICommand CmdSendMessage
        {
            get
            {
                return _cmdSendMessage ??
                    (_cmdSendMessage = new RelayCommand<string>(
                        (msg) =>
                        {
                            _ccs.SendMessage(msg, SelectedAccount.IsCaster);
                            MessageToSend = "";
                        },
                        (msg) =>
                        {
                            // Can execute when not null or whitespace
                            return !string.IsNullOrWhiteSpace(msg) && SelectedAccount != null;
                        }
                    ));
            }
        }

        #endregion

    }
}