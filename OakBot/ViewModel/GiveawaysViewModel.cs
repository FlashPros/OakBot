﻿using System;
using System.Collections.ObjectModel;

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;

using OakBot.Model;

namespace OakBot.ViewModel
{
    /* TODO: Manually add / remove giveaway modules as per user
     * Load back in amount of used modules used on prior run of the bot.
     */

    public class GiveawaysViewModel : ViewModelBase
    {
        #region Fields

        private readonly IChatConnectionService _chatService;
        private readonly IWebSocketEventService _wsEventService;

        private readonly Random _rnd;
        private ObservableCollection<GiveawayViewModel> _modules;
        private bool _isChatConnected;

        #endregion

        #region Constructors

        public GiveawaysViewModel(IChatConnectionService chatService, IWebSocketEventService wsEventService)
        {
            // Store references to services
            _chatService = chatService;
            _wsEventService = wsEventService;

            // Subscribe to system messages
            Messenger.Default.Register<bool>(this, "SystemChatConnectionChanged", (status) => SystemChatConnectionChanged(status));

            // Use one seeded pseudo-random generator for all giveaway modules
            _rnd = new Random();

            // Set giveaway modules
            _modules = new ObservableCollection<GiveawayViewModel>
            {
                new GiveawayViewModel(1, _rnd, _chatService, _wsEventService),
                new GiveawayViewModel(2, _rnd, _chatService, _wsEventService),
                new GiveawayViewModel(3, _rnd, _chatService, _wsEventService)
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// System chat connection changed handler.
        /// </summary>
        /// <param name="status">Status of the system chat connection.</param>
        private void SystemChatConnectionChanged(bool status)
        {
            IsChatConnected = status;
        }

        #endregion

        #region Properties

        public ObservableCollection<GiveawayViewModel> GiveawayModules
        {
            get
            {
                return _modules;
            }
        }

        public bool IsChatConnected
        {
            get
            {
                return _isChatConnected;
            }
            set
            {
                if (value != _isChatConnected)
                {
                    _isChatConnected = value;
                    RaisePropertyChanged();
                }
            }
        }

        #endregion
    }
}
