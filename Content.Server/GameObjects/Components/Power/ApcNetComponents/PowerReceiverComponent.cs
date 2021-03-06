﻿using Content.Server.GameObjects.Components.NodeContainer.NodeGroups;
using Content.Shared.GameObjects.Components.Power;
using Robust.Server.GameObjects;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using System;
using Robust.Shared.GameObjects.Components;

namespace Content.Server.GameObjects.Components.Power.ApcNetComponents
{
    /// <summary>
    ///     Attempts to link with a nearby <see cref="IPowerProvider"/>s so that it can receive power from a <see cref="IApcNet"/>.
    /// </summary>
    [RegisterComponent]
    public class PowerReceiverComponent : Component
    {
        public override string Name => "PowerReceiver";

        public event EventHandler<PowerStateEventArgs> OnPowerStateChanged;

        [ViewVariables]
        public bool Powered => (HasApcPower || !NeedsPower) && !PowerDisabled;

        [ViewVariables]
        public bool HasApcPower { get => _hasApcPower; set => SetHasApcPower(value); }
        private bool _hasApcPower;

        /// <summary>
        ///     The max distance from a <see cref="PowerProviderComponent"/> that this can receive power from.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public int PowerReceptionRange { get => _powerReceptionRange; set => SetPowerReceptionRange(value); }
        private int _powerReceptionRange;

        [ViewVariables]
        public IPowerProvider Provider { get => _provider; set => SetProvider(value); }
        private IPowerProvider _provider = PowerProviderComponent.NullProvider;

        /// <summary>
        ///     If this should be considered for connection by <see cref="PowerProviderComponent"/>s.
        /// </summary>
        public bool Connectable => Anchored;

        private bool Anchored => !Owner.TryGetComponent<PhysicsComponent>(out var physics) || physics.Anchored;

        [ViewVariables]
        public bool NeedsProvider { get; private set; } = true;

        /// <summary>
        ///     Amount of charge this needs from an APC per second to function.
        /// </summary>
        [ViewVariables]
        public int Load { get => _load; set => SetLoad(value); }
        private int _load;

        /// <summary>
        ///     When true, causes this to appear powered even if not receiving power from an Apc.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool NeedsPower { get => _needsPower; set => SetNeedsPower(value); }
        private bool _needsPower;

        /// <summary>
        ///     When true, causes this to never appear powered.
        /// </summary>
        public bool PowerDisabled { get => _powerDisabled; set => SetPowerDisabled(value); }
        private bool _powerDisabled;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _powerReceptionRange, "powerReceptionRange", 3);
            serializer.DataField(ref _load, "powerLoad", 5);
            serializer.DataField(ref _needsPower, "needsPower", true);
            serializer.DataField(ref _powerDisabled, "powerDisabled", false);
        }

        protected override void Startup()
        {
            base.Startup();
            if (NeedsProvider)
            {
                TryFindAndSetProvider();
            }
            if (Owner.TryGetComponent<PhysicsComponent>(out var physics))
            {
                AnchorUpdate();
                physics.AnchoredChanged += AnchorUpdate;
            }
        }

        public override void OnRemove()
        {
            if (Owner.TryGetComponent<PhysicsComponent>(out var physics))
            {
                physics.AnchoredChanged -= AnchorUpdate;
            }
            _provider.RemoveReceiver(this);
            base.OnRemove();
        }

        public void TryFindAndSetProvider()
        {
            if (TryFindAvailableProvider(out var provider))
            {
                Provider = provider;
            }
        }

        private bool TryFindAvailableProvider(out IPowerProvider foundProvider)
        {
            var nearbyEntities = IoCManager.Resolve<IServerEntityManager>()
                .GetEntitiesInRange(Owner, PowerReceptionRange);
            var mapManager = IoCManager.Resolve<IMapManager>();
            foreach (var entity in nearbyEntities)
            {
                if (entity.TryGetComponent<PowerProviderComponent>(out var provider))
                {
                    if (provider.Connectable)
                    {
                        var distanceToProvider = provider.Owner.Transform.GridPosition.Distance(mapManager, Owner.Transform.GridPosition);
                        if (distanceToProvider < Math.Min(PowerReceptionRange, provider.PowerTransferRange))
                        {
                            foundProvider = provider;
                            return true;
                        }
                    }
                }
            }
            foundProvider = default;
            return false;
        }

        public void ClearProvider()
        {
            _provider.RemoveReceiver(this);
            _provider = PowerProviderComponent.NullProvider;
            NeedsProvider = true;
            HasApcPower = false;
        }

        private void SetProvider(IPowerProvider newProvider)
        {
            _provider.RemoveReceiver(this);
            _provider = newProvider;
            newProvider.AddReceiver(this);
            NeedsProvider = false;
        }

        private void SetHasApcPower(bool newHasApcPower)
        {
            var oldPowered = Powered;
            _hasApcPower = newHasApcPower;
            if (oldPowered != Powered)
            {
                OnNewPowerState();
            }
        }

        private void SetPowerReceptionRange(int newPowerReceptionRange)
        {
            ClearProvider();
            _powerReceptionRange = newPowerReceptionRange;
            TryFindAndSetProvider();
        }

        private void SetLoad(int newLoad)
        {
            _load = newLoad;
        }

        private void SetNeedsPower(bool newNeedsPower)
        {
            var oldPowered = Powered;
            _needsPower = newNeedsPower;
            if (oldPowered != Powered)
            {
                OnNewPowerState();
            }
        }

        private void SetPowerDisabled(bool newPowerDisabled)
        {
            var oldPowered = Powered;
            _powerDisabled = newPowerDisabled;
            if (oldPowered != Powered)
            {
                OnNewPowerState();
            }
        }

        private void OnNewPowerState()
        {
            OnPowerStateChanged?.Invoke(this, new PowerStateEventArgs(Powered));
            if (Owner.TryGetComponent(out AppearanceComponent appearance))
            {
                appearance.SetData(PowerDeviceVisuals.Powered, Powered);
            }
        }

        private void AnchorUpdate()
        {
            if (Anchored)
            {
                if (NeedsProvider)
                {
                    TryFindAndSetProvider();
                }
            }
            else
            {
                ClearProvider();
            }
        }
    }

    public class PowerStateEventArgs : EventArgs
    {
        public readonly bool Powered;

        public PowerStateEventArgs(bool powered)
        {
            Powered = powered;
        }
    }
}
