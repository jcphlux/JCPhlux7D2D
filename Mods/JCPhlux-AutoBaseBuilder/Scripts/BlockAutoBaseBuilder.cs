using System;

public class BlockAutoBaseBuilder : BlockSecureLoot
{
    private float BuildSpeed = 5;
    private Vector2i LootSize = new(8, 4);

    //private float BoundHelperSize = 2.59f;
    private float TakeDelay = 20f;

    public override string GetActivationText(
        WorldBase _world,
        BlockValue _blockValue,
        int _clrIdx,
        Vector3i _blockPos,
        EntityAlive _entityFocusing)
    {
        return base.GetActivationText(_world, _blockValue, _clrIdx, _blockPos, _entityFocusing);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(
        WorldBase _world,
        BlockValue _blockValue,
        int _clrIdx,
        Vector3i _blockPos,
        EntityAlive _entityFocusing)
    {
        TileEntityAutoBaseBuilder tileEntity = _world.GetTileEntity(_clrIdx, _blockPos) as TileEntityAutoBaseBuilder;
        BlockActivationCommand[] cmds = base.GetBlockActivationCommands(_world, _blockValue, _clrIdx, _blockPos, _entityFocusing);
        Array.Resize(ref cmds, cmds.Length + 3);

        cmds[cmds.Length - 2] = new BlockActivationCommand("take", "hand", false);
        string activate_cmd = tileEntity.IsOn ? "turn_autobuild_off" : "turn_autobuild_on";
        cmds[cmds.Length - 1] = new BlockActivationCommand(activate_cmd, "electric_switch", true);
        if (this.CanPickup)
            cmds[cmds.Length - 2].enabled = true;
        else if ((double)EffectManager.GetValue(PassiveEffects.BlockPickup, _entity: _entityFocusing, tags: _blockValue.Block.Tags) > 0.0)
            cmds[cmds.Length - 2].enabled = true;
        else
            cmds[cmds.Length - 2].enabled = false;

        //string prefablist_cmd = "select_abb_prefab";
        //if (!tileEntity.prefabLocation.Equals(PathAbstractions.AbstractedLocation.None))
        //{
        //    prefablist_cmd = Localization.Get("blockcommand_selected_abb_prefab");
        //    if (string.IsNullOrEmpty(prefablist_cmd)) prefablist_cmd = "Selected Prefab {0}";
        //    prefablist_cmd = string.Format(prefablist_cmd, tileEntity.prefabLocation.Name);
        //}
        cmds[cmds.Length - 3] = new BlockActivationCommand("select_abb_prefab", "map_town", true);

        return cmds;
    }

    public override void Init()
    {
        base.Init();
        TakeDelay = !Properties.Values.ContainsKey("TakeDelay") ? TakeDelay
            : StringParsers.ParseFloat(Properties.Values["TakeDelay"]);
        BuildSpeed = !Properties.Values.ContainsKey("BuildSpeed") ? BuildSpeed
            : StringParsers.ParseFloat(Properties.Values["BuildSpeed"]);
        AllowedRotations = EBlockRotationClasses.Basic90;
        Log.Out("Allowed rotations: " + AllowedRotations.ToString());
    }

    //public override void OnBlockUnloaded(
    //    WorldBase _world,
    //    int _clrIdx,
    //    Vector3i _blockPos,
    //    BlockValue _blockValue)
    //{
    //    base.OnBlockUnloaded(_world, _clrIdx, _blockPos, _blockValue);
    //    if (_world.GetTileEntity(_clrIdx, _blockPos) is TileEntityAutoBaseBuilder tileEntityLandAutoRepair)
    //    {
    //        LandClaimBoundsHelper.RemoveBoundsHelper(_blockPos.ToVector3());
    //    }
    //}
    public override bool OnBlockActivated(
        string _commandName,
        WorldBase _world,
        int _cIdx,
        Vector3i _blockPos,
        BlockValue _blockValue,
        EntityAlive _player)
    {
        if (_world.GetTileEntity(_cIdx, _blockPos) is not TileEntityAutoBaseBuilder tileEntity) return false;

        bool hasPrefab = !tileEntity.prefabLocation.Equals(PathAbstractions.AbstractedLocation.None);

        if (_commandName == "take")
        {
            bool flag = this.CanPickup;
            if ((double)EffectManager.GetValue(PassiveEffects.BlockPickup, _entity: _player, tags: _blockValue.Block.Tags) > 0.0)
                flag = true;
            if (!flag) return false;
            if (!_world.CanPickupBlockAt(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer()))
            {
                _player.PlayOneShot("keystone_impact_overlay");
                return false;
            }
            if (tileEntity.IsOn)
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttTurnOffAutoBuildBeforePickup"), "ui_denied");
                return false;
            }
            if (hasPrefab)
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttPrefabProgressLossBeforePickup"), "ui_denied");
            }
            if (_blockValue.damage > 0)
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttRepairBeforePickup"), "ui_denied");
                return false;
            }
            ItemStack itemStack = Block.list[_blockValue.type].OnBlockPickedUp(_world, _cIdx, _blockPos, _blockValue, _player.entityId);
            if (!_player.inventory.CanTakeItem(itemStack) && !_player.bag.CanTakeItem(itemStack))
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("xuiInventoryFullForPickup"), "ui_denied");
                return false;
            }
            TakeItemWithTimer(_cIdx, _blockPos, _blockValue, _player);
            return false;
        }
        else if (_commandName == "turn_autobuild_off")
        {
            tileEntity.IsOn = false;
            return true;
        }
        else if (_commandName == "turn_autobuild_on")
        {
            if (!hasPrefab)
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttNoPrefabSelected"), "ui_denied");
                return false;
            }
            tileEntity.IsOn = true;
            return true;
        }
        else if (_commandName == "select_abb_prefab")
        {
            if (tileEntity.IsOn)
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttTurnOffAutoBuildFirst"), "ui_denied");
                return false;
            }

            LocalPlayerUI playerUi = (_player as EntityPlayerLocal).PlayerUI;

            GUIWindow gUI = playerUi.windowManager.GetWindow("uiABBPrefabList");
            XUiC_ABBPrefabList prefabList = ((XUiWindowGroup)gUI).Controller.GetChildByType<XUiC_ABBPrefabList>();

            if (hasPrefab)
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttPrefabAlreadySelected"), "ui_denied");

            prefabList.SetTileEntity(tileEntity);
            playerUi.windowManager.Open(gUI, true);
            return true;
        }
        else
        {
            return base.OnBlockActivated(_commandName, _world, _cIdx, _blockPos, _blockValue, _player);
        }
    }

    //public override void OnBlockLoaded(
    //    WorldBase _world,
    //    int _clrIdx,
    //    Vector3i _blockPos,
    //    BlockValue _blockValue)
    //{
    //    base.OnBlockLoaded(_world, _clrIdx, _blockPos, _blockValue);
    //    if (GameManager.IsDedicatedServer) return;
    //    if (_world.GetTileEntity(_clrIdx, _blockPos) is TileEntityAutoBaseBuilder tileEntityLandAutoRepair)
    //    {
    //        Transform boundsHelper = LandClaimBoundsHelper.GetBoundsHelper(_blockPos.ToVector3());
    //        if (boundsHelper != null)
    //        {
    //            boundsHelper.localScale = new Vector3(BoundHelperSize, BoundHelperSize, BoundHelperSize);
    //            boundsHelper.localPosition = new Vector3(_blockPos.x + 0.5f, _blockPos.y + 0.5f, _blockPos.z + 0.5f);
    //            //tileEntityLandAutoRepair.BoundsHelper = boundsHelper;
    //            //tileEntityLandAutoRepair.ResetBoundHelper(Color.gray);
    //        }
    //    }
    //}
    public override void OnBlockAdded(
        WorldBase _world,
        Chunk _chunk,
        Vector3i _blockPos,
        BlockValue _blockValue)
    {
        if (_blockValue.ischild || _world.GetTileEntity(_chunk.ClrIdx, _blockPos) is TileEntityAutoBaseBuilder)
            return;

        TileEntityAutoBaseBuilder tileEntity = new TileEntityAutoBaseBuilder(_chunk)
        {
            localChunkPos = World.toBlock(_blockPos),
            lootListName = lootList
        };
        tileEntity.buildSpeed = BuildSpeed;
        tileEntity.SetContainerSize(LootSize, false);
        _chunk.AddTileEntity(tileEntity);

        base.OnBlockAdded(_world, _chunk, _blockPos, _blockValue);
        if (GameManager.IsDedicatedServer) return;
        //if (_world.GetTileEntity(_chunk.ClrIdx, _blockPos) is TileEntityAutoBaseBuilder tileEntityLandAutoRepair)
        //{
        //    Transform boundsHelper = LandClaimBoundsHelper.GetBoundsHelper(_blockPos.ToVector3());
        //    if (boundsHelper != null)
        //    {
        //        boundsHelper.localScale = new Vector3(BoundHelperSize, BoundHelperSize, BoundHelperSize);
        //        boundsHelper.localPosition = new Vector3(_blockPos.x + 0.5f, _blockPos.y + 0.5f, _blockPos.z + 0.5f);
        //        //tileEntityLandAutoRepair.BoundsHelper = boundsHelper;
        //        //tileEntityLandAutoRepair.ResetBoundHelper(Color.gray);
        //    }
        //}
    }

    public override void OnBlockEntityTransformAfterActivated(
                WorldBase _world,
        Vector3i _blockPos,
        int _cIdx,
        BlockValue _blockValue,
        BlockEntityData _ebcd)
    {
        if (_ebcd == null) return;
        if (_world.GetTileEntity(_cIdx, _blockPos) is TileEntityAutoBaseBuilder te)
        {
            te.buildSpeed = BuildSpeed;
        }
        else
        {
            Chunk chunkFromWorldPos = (Chunk)_world.GetChunkFromWorldPos(_blockPos);
            te = new TileEntityAutoBaseBuilder(chunkFromWorldPos)
            {
                localChunkPos = World.toBlock(_blockPos),
                lootListName = lootList
            };
            te.buildSpeed = BuildSpeed;
            te.SetContainerSize(LootSize, false);
            chunkFromWorldPos.AddTileEntity(te);
        }

        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _cIdx, _blockValue, _ebcd);
    }

    //public new void RotateHoldingBlock(
    //                    ItemClassBlock.ItemBlockInventoryData _blockInventoryData,
    //bool _increaseRotation,
    //bool _playSoundOnRotation = true)
    //{
    //    Log.Out("RotateHoldingBlock" + _blockInventoryData.mode);
    //    if (_blockInventoryData.mode == BlockPlacement.EnumRotationMode.Auto)
    //        _blockInventoryData.mode = BlockPlacement.EnumRotationMode.Simple;
    //    BlockValue blockValue1 = _blockInventoryData.itemValue.ToBlockValue() with
    //    {
    //        rotation = _blockInventoryData.rotation
    //    };
    //    BlockValue blockValue2 = this.BlockPlacementHelper.OnPlaceBlock(_blockInventoryData.mode, _blockInventoryData.localRot, (WorldBase)_blockInventoryData.world, blockValue1, _blockInventoryData.hitInfo.hit, _blockInventoryData.holdingEntity.position).blockValue;
    //    int rotation = (int)_blockInventoryData.rotation;
    //    _blockInventoryData.rotation = this.BlockPlacementHelper.LimitRotation(_blockInventoryData.mode, ref _blockInventoryData.localRot, _blockInventoryData.hitInfo.hit, _increaseRotation, blockValue2, blockValue2.rotation);
    //    if (!_playSoundOnRotation || rotation == (int)_blockInventoryData.rotation)
    //        return;
    //    _blockInventoryData.holdingEntity.PlayOneShot("rotateblock");
    //}

    //public override void OnBlockRemoved(
    //    WorldBase _world,
    //    Chunk _chunk,
    //    Vector3i _blockPos,
    //    BlockValue _blockValue)
    //{
    //    base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
    //    //if (_world.GetTileEntity(_chunk.ClrIdx, _blockPos) is TileEntityAutoBaseBuilder tileEntityLandAutoRepair)
    //    //{
    //    //    LandClaimBoundsHelper.RemoveBoundsHelper(_blockPos.ToVector3());
    //    //}
    //}
    public void TakeItemWithTimer(
        int _cIdx,
        Vector3i _blockPos,
        BlockValue _blockValue,
        EntityAlive _player)
    {
        if (_blockValue.damage > 0)
        {
            GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttRepairBeforePickup"), "ui_denied");
        }
        else
        {
            LocalPlayerUI playerUi = (_player as EntityPlayerLocal).PlayerUI;
            playerUi.windowManager.Open("timer", true);
            XUiC_Timer childByType = playerUi.xui.GetChildByType<XUiC_Timer>();
            TimerEventData _eventData = new TimerEventData();
            _eventData.Data = new object[4]
            {
                _cIdx,
                _blockValue,
                _blockPos,
                _player
            };
            _eventData.Event += new TimerEventHandler(EventData_Event);
            childByType.SetTimer(TakeDelay, _eventData);
        }
    }

    protected virtual void HandleTakeInternalItems(TileEntityAutoBaseBuilder te, LocalPlayerUI playerUI)
    {
        ItemStack[] items = te.items;
        for (int index = 0; index < items.Length; ++index)
        {
            if (!items[index].IsEmpty() && !playerUI.xui.PlayerInventory.AddItem(items[index]))
                playerUI.xui.PlayerInventory.DropItem(items[index]);
        }
    }

    private void EventData_Event(TimerEventData timerData)
    {
        World world = GameManager.Instance.World;
        object[] data = (object[])timerData.Data;
        int _clrIdx = (int)data[0];
        BlockValue blockValue = (BlockValue)data[1];
        Vector3i vector3i = (Vector3i)data[2];
        BlockValue block = world.GetBlock(vector3i);
        EntityPlayerLocal entityPlayerLocal = data[3] as EntityPlayerLocal;
        if (block.damage > 0)
        {
            GameManager.ShowTooltip(entityPlayerLocal, Localization.Get("ttRepairBeforePickup"), "ui_denied");
        }
        else if (block.type != blockValue.type)
        {
            GameManager.ShowTooltip(entityPlayerLocal, Localization.Get("ttBlockMissingPickup"), "ui_denied");
        }
        else
        {
            TileEntityAutoBaseBuilder tileEntity = world.GetTileEntity(_clrIdx, vector3i) as TileEntityAutoBaseBuilder;
            if (tileEntity.IsUserAccessing())
            {
                GameManager.ShowTooltip(entityPlayerLocal, Localization.Get("ttCantPickupInUse"), "ui_denied");
            }
            else
            {
                LocalPlayerUI uiForPlayer = LocalPlayerUI.GetUIForPlayer(entityPlayerLocal);
                HandleTakeInternalItems(tileEntity, uiForPlayer);
                ItemStack itemStack = new ItemStack(block.ToItemValue(), 1);
                if (!uiForPlayer.xui.PlayerInventory.AddItem(itemStack))
                    uiForPlayer.xui.PlayerInventory.DropItem(itemStack);
                world.SetBlockRPC(_clrIdx, vector3i, BlockValue.Air);
            }
        }
    }
}