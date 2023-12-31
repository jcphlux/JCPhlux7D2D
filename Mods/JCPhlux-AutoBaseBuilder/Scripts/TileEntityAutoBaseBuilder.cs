using System.Collections.Generic;
using UnityEngine;

public class TileEntityAutoBaseBuilder : TileEntitySecureLootContainer
{
    // The acquired block to be built
    public BlockValue buildBlock = BlockValue.Air;

    // The position of the block being built
    public Vector3i buildPosition = Vector3i.zero;

    public float buildSpeed = 5;

    // Flag only for server side code
    public bool isAccessed;

    // Copied from LandClaim code
    //public Transform BoundsHelper;

    // The selected prefab to be built
    public PathAbstractions.AbstractedLocation prefabLocation = PathAbstractions.AbstractedLocation.None;

    // The offset of the prefab to be built
    public Vector3i prefabOffset = Vector3i.zero;

    // The rotation of the prefab to be built
    public byte? prefabRotation = null;

    // The tick counter for next block build
    private float buildTicks = 750f;

    private bool isOn;
    private string lastMissingItem = null;

    public TileEntityAutoBaseBuilder(Chunk _chunk)
            : base(_chunk)
    {
        isOn = false;
        isAccessed = false;
        buildBlock = BlockValue.Air;

        //var prefabList = new XUiC_PrefabList();
        // prefabList.
        //PathAbstractions.AbstractedLocation x = PathAbstractions.PrefabsSearchPaths.GetLocation("PrefabTest");
    }

    // Some basic stats from searches
    //bool hadDamagedBlock = false;
    //bool hadBlockOutside = false;
    public bool IsOn
    {
        get => isOn;
        set
        {
            if (isOn != value)
            {
                isOn = value;
                buildBlock = BlockValue.Air;
                buildPosition = ToWorldPos();
                //ResetBoundHelper(Color.gray);
                SetModified();
            }
        }
    }

    public byte Rotation => UtilsHelpers.NormalizeSimpleRotation(blockValue.rotation);

    public int GetItemCount(Block.SItemNameCount sitem)
    {
        int having = 0;
        for (int i = 0; i < items.Length; i++)
        {
            ItemStack stack = items[i];
            if (stack.IsEmpty()) continue;
            // ToDo: how expensive is this call for `GetItem(string)`?
            if (stack.itemValue.type == ItemClass.GetItem(sitem.ItemName).type)
            {
                // Always leave at least one item in the slot
                having += stack.count;
            }
        }
        return having;
    }

    public override TileEntityType GetTileEntityType() => (TileEntityType)189;

    public override void read(PooledBinaryReader _br, TileEntity.StreamModeRead _eStreamMode)
    {
        base.read(_br, _eStreamMode);
        isOn = _br.ReadBoolean();
        Log.Out("_eStreamMode: " + _eStreamMode.ToString());
        switch (_eStreamMode)
        {
            case TileEntity.StreamModeRead.Persistency:
                break;

            case TileEntity.StreamModeRead.FromServer:
                bool hasPrefab = _br.ReadBoolean();
                if (!hasPrefab)
                    break;

                string prefabName = _br.ReadString();
                Log.Out("prefabName: " + prefabName);
                prefabLocation = PathAbstractions.PrefabsSearchPaths.GetLocation(prefabName);
                Log.Out("prefabLocation: " + prefabLocation.ToString());
                prefabRotation = _br.ReadByte();

                prefabOffset.x = _br.ReadInt32();
                prefabOffset.y = _br.ReadInt32();
                prefabOffset.z = _br.ReadInt32();

                buildPosition.x = _br.ReadInt32();
                buildPosition.y = _br.ReadInt32();
                buildPosition.z = _br.ReadInt32();

                //bool isBuilding = _br.ReadBoolean();

                buildTicks = _br.ReadInt32();

                lastMissingItem = _br.ReadBoolean() ? _br.ReadString() : null;
                //float progress = _br.ReadSingle();
                //if (isOn && isBuilding)
                //{
                //    EnableBoundHelper(progress);
                //}
                //else if (hadBlockOutside)
                //{
                //    ResetBoundHelper(Color.red);
                //}
                //else if (hadDamagedBlock)
                //{
                //    ResetBoundHelper(orange);
                //}
                //else
                //{
                //    ResetBoundHelper(Color.gray);
                //}
                break;

            case TileEntity.StreamModeRead.FromClient:
                isAccessed = _br.ReadBoolean();
                if (isAccessed)
                {
                    // This will provoke an update on all clients to know new state.
                    ResetAcquiredBlock("weapon_jam");
                }
                break;
        }
    }

    public void ReduceItemCount(Block.SItemNameCount sitem, int count)
    {
        for (int i = 0; i < items.Length; i++)
        {
            ItemStack stack = items[i];
            if (stack.IsEmpty()) continue;
            // ToDo: how expensive is this call for `GetItem(string)`?
            if (stack.itemValue.type == ItemClass.GetItem(sitem.ItemName).type)
            {
                if (count <= stack.count)
                {
                    stack.count -= count;
                    UpdateSlot(i, stack);
                    return;
                }
                else
                {
                    count -= stack.count;
                    stack.count = 0;
                    UpdateSlot(i, stack);
                }
            }
        }
    }

    public void ResetAcquiredBlock(string playSound = "", bool broadcast = true)
    {
        if (buildBlock.type != BlockValue.Air.type)
        {
            // Play optional sound (only at the server to broadcast everywhere)
            if (playSound != "" && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                GameManager.Instance.PlaySoundAtPositionServer(
                    ToWorldPos().ToVector3(), playSound,
                    AudioRolloffMode.Logarithmic, 100);
            }
            // Reset acquired build block
            buildBlock = BlockValue.Air;
            buildPosition = ToWorldPos();
            buildTicks = buildSpeed;
            //ResetBoundHelper(Color.gray);
            if (broadcast)
            {
                SetModified();
            }
        }
    }

    public void SetPrefab(PathAbstractions.AbstractedLocation location, Vector3i offset, byte rotation)
    {
        IsOn = true;
        if (prefabLocation.Equals(location) && prefabOffset == offset && prefabRotation == rotation)
        {
            IsOn = true;
            return;
        }
        prefabLocation = location;
        prefabOffset = offset;
        prefabRotation = rotation;
        isOn = true;
        SetModified();
    }

    //public bool CanRepairBlock(Block block)
    //{
    //    if (block.RepairItems == null) return false;
    //    for (int i = 0; i < block.RepairItems.Count; i++)
    //    {
    //        int needed = block.RepairItems[i].Count;
    //        needed = (int)Mathf.Ceil(damagePerc * needed);
    //        int available = GetItemCount(block.RepairItems[i]);
    //        if (available < needed)
    //        {
    //            if (available == 0) lastMissingItem =
    //                    block.RepairItems[i].ItemName;
    //            return false;
    //        }
    //    }

    //    return block.RepairItems.Count > 0;
    //}

    //public bool TakeRepairMaterials(Block block)
    //{
    //    if (block.RepairItems == null) return false;
    //    for (int i = 0; i < block.RepairItems.Count; i++)
    //    {
    //        int needed = block.RepairItems[i].Count;
    //        needed = (int)Mathf.Ceil( damagePerc * needed);
    //        ReduceItemCount(block.RepairItems[i], needed);
    //    }

    //    return true;
    //}

    //public Vector3i GetRandomPos(World world, Vector3 pos, int size)
    //{
    //    int x = 0; int y = 0; int z = 0;
    //    // We don't fix ourself!
    //    while (x == 0 && y == 0 && z == 0 && size != 0)
    //    {
    //        x = Random.Range(-size, size);
    //        y = Random.Range(-size, size);
    //        z = Random.Range(-size, size);
    //    }
    //    return new Vector3i(
    //        pos.x + x,
    //        pos.y + y,
    //        pos.z + z
    //    );

    //}

    //static Color orange = new Color(1f, 0.6f, 0f);
    public override void SetUserAccessing(bool _bUserAccessing)
    {
        if (IsUserAccessing() != _bUserAccessing)
        {
            base.SetUserAccessing(_bUserAccessing);
            //hadDamagedBlock = false;
            //hadBlockOutside = false;
            if (_bUserAccessing)
            {
                if (lastMissingItem != null)
                {
                    var player = GameManager.Instance?.World?.GetPrimaryPlayer();
                    string msg = Localization.Get("xuiBlockABBMissed");
                    if (string.IsNullOrEmpty(msg)) msg = "Base Auto Builder could use {0}";
                    msg = string.Format(msg, ItemClass.GetItemClass(lastMissingItem).GetLocalizedItemName());
                    GameManager.Instance.ChatMessageServer(
                        (ClientInfo)null,
                        EChatType.Whisper,
                        player.entityId,
                        msg,
                        string.Empty, false,
                        new List<int> { player.entityId });
                    lastMissingItem = null;
                }

                ResetAcquiredBlock("weapon_jam", false);
                SetModified(); // Force update
            }
            // SetModified is already called OnClose
        }
    }

    public void TickBuild(World world)
    {
        if (buildSpeed-- <= 0)
        {
            buildSpeed = 5;
            Log.Out("TickBuild");
        }
        //WorldBiomeProviderFromImage.GetBiomeAt(int x, int z)
        //WorldBiomeProviderFromImage
        //    Vector3i worldPosI = ToWorldPos();
        //    Vector3 worldPos = ToWorldPos().ToVector3();

        // // ToDo: probably don't need to recalculate on each tick since we reset on damage changes
        // damagePerc = (float)buildBlock.damage / (float)Block.list[buildBlock.type].MaxDamage;

        // // Check if we have a block for repair acquired if (buildBlock.type !=
        // BlockValue.Air.type) { // Get block currently at the position we try to repair BlockValue
        // currentValue = world.GetBlock(buildPosition); // Check if any of the stats changed after
        // we acquired to block if (currentValue.type != buildBlock.type || currentValue.damage !=
        // buildBlock.damage) { // Reset the acquired block and play a sound bit // Play different
        // sound according to reason of disconnect // Block has been switched (maybe destroyed,
        // upgraded, etc.) // Block has been damaged again, abort repair on progress
        // ResetAcquiredBlock(currentValue.type != buildBlock.type ? "weapon_jam" :
        // "ItemNeedsRepair"); return; }

        // // Increase amount of repairing done repairDamage += Time.deltaTime * buildSpeed; //
        // Check if repaired enough to fully restore if (buildBlock.damage <= repairDamage) { //
        // Safety check if materials have changed if (!CanRepairBlock(Block.list[buildBlock.type]))
        // { // Inventory seems to have changed (not repair possible)
        // ResetAcquiredBlock("weapon_jam"); return; } // Need to get the chunk first in order to
        // alter the block? if (world.GetChunkFromWorldPos(buildPosition) is Chunk
        // chunkFromWorldPos) { // Completely restore the block buildBlock.damage = 0; // Update the
        // block at the given position (very low-level function) // Note: with this function we can
        // basically install a new block at position world.SetBlock(chunkFromWorldPos.ClrIdx,
        // buildPosition, buildBlock, false, false); // Take the repair materials from the container
        // // ToDo: what if materials have gone missing? TakeRepairMaterials(buildBlock.Block); //
        // BroadCast the changes done to the block world.SetBlockRPC(chunkFromWorldPos.ClrIdx,
        // buildPosition, buildBlock, buildBlock.Block.Density); // Update the bound helper (maybe
        // debounce a little?) EnableBoundHelper(repairDamage / buildBlock.damage); // Get material
        // to play material specific sound var material =
        // buildBlock.Block.blockMaterial.SurfaceCategory;
        // world.GetGameManager().PlaySoundAtPositionServer( buildPosition.ToVector3(), // or at
        // `worldPos`? string.Format("ImpactSurface/metalhit{0}", material),
        // AudioRolloffMode.Logarithmic, 100); // Update clients SetModified(); } // Reset acquired
        // block ResetAcquiredBlock(); } else { EnableBoundHelper(repairDamage / buildBlock.damage);
        // // Play simple click indicating we are working on something
        // world.GetGameManager().PlaySoundAtPositionServer(worldPos, "repair_block",
        // AudioRolloffMode.Logarithmic, 100); }

        // } else { // Get size of land claim blocks to look for valid blocks to repair int
        // claimSize = (GameStats.GetInt(EnumGameStats.LandClaimSize) - 1) / 2;

        // // Check if block is within a land claim block (don't repair stuff outside) // ToDo: Not
        // sure if this is the best way to check this, but it should work PersistentPlayerList
        // persistentPlayerList = world.GetGameManager().GetPersistentPlayerList();
        // PersistentPlayerData playerData = persistentPlayerList.GetPlayerData(this.GetOwner());

        // // Speed up finding of blocks (for easier debugging purpose only!) // int n = 0; while
        // (++n < 500 && repairBlock.type == BlockValue.Air.type)

        // // Simple and crude random block acquiring // Repair block has slightly further reach for
        // (int i = 1; i <= claimSize + 5; i += 1) { // Get a random block and see if it need repair
        // Vector3i randomPos = GetRandomPos(world, worldPos, i); BlockValue blockValue = world.GetBlock(randomPos);

        // damagePerc = (float)(blockValue.damage) / (float)(Block.list[blockValue.type].MaxDamage);

        // // Check if block needs repair and if we have the needed materials if (blockValue.damage
        // > 0) { if (CanRepairBlock(blockValue.Block)) { // int deadZone =
        // GameStats.GetInt(EnumGameStats.LandClaimDeadZone) + claimSize; Chunk chunkFromWorldPos =
        // (Chunk)world.GetChunkFromWorldPos(worldPosI); if (!IsBlockInsideClaim(world,
        // chunkFromWorldPos, randomPos, playerData, claimSize, true)) { // Check if the block is
        // close by, which suggests a missing land claim block? if (Mathf.Abs(randomPos.x -
        // worldPos.x) < claimSize / 2) hadBlockOutside = true; else if (Mathf.Abs(randomPos.y -
        // worldPos.y) < claimSize / 2) hadBlockOutside = true; else if (Mathf.Abs(randomPos.z -
        // worldPos.z) < claimSize / 2) hadBlockOutside = true; // Skip it continue; } // Play
        // simple click indicating we are working on something
        // world.GetGameManager().PlaySoundAtPositionServer(worldPos, "timer_stop",
        // AudioRolloffMode.Logarithmic, 100); // Acquire the block to repair buildPosition =
        // randomPos; buildBlock = blockValue; repairDamage = 0.0f; hadDamagedBlock = false;
        // hadBlockOutside = false; EnableBoundHelper(0); SetModified(); return; } else if
        // (blockValue.Block?.RepairItems?.Count > 0) { hadDamagedBlock = true; } } }

        // if (hadBlockOutside) { lastMissingItem = "keystoneBlock"; ResetBoundHelper(Color.red);
        // SetModified(); } else if (hadDamagedBlock) { ResetBoundHelper(orange); SetModified(); }
        // else if (buildPosition == worldPos) { ResetBoundHelper(Color.gray); SetModified(); }

        // }
    }

    public override void UpdateTick(World world)
    {
        base.UpdateTick(world);

        // Check if storage is being accessed
        if (!IsOn || IsUserAccessing() || isAccessed)
        {
            ResetAcquiredBlock("weapon_jam");
        }
        else
        {
            // Call regular Tick
            TickBuild(world);
        }
    }

    public override void write(PooledBinaryWriter _bw, TileEntity.StreamModeWrite _eStreamMode)
    {
        base.write(_bw, _eStreamMode);
        _bw.Write(isOn);
        Log.Out("_eStreamMode: " + _eStreamMode.ToString());
        switch (_eStreamMode)
        {
            case TileEntity.StreamModeWrite.Persistency:
                break;

            case TileEntity.StreamModeWrite.ToServer:
                _bw.Write(IsUserAccessing());
                break;

            case TileEntity.StreamModeWrite.ToClient:
                bool hasPrefab = !prefabLocation.Equals(PathAbstractions.AbstractedLocation.None);
                Log.Out("hasPrefab: " + hasPrefab);
                _bw.Write(hasPrefab);
                if (!hasPrefab)
                    break;
                _bw.Write(prefabLocation.Name);
                _bw.Write(prefabRotation.Value);

                _bw.Write(prefabOffset.x);
                _bw.Write(prefabOffset.y);
                _bw.Write(prefabOffset.z);

                _bw.Write(buildPosition.x);
                _bw.Write(buildPosition.y);
                _bw.Write(buildPosition.z);

                //_bw.Write(buildBlock.type != BlockValue.Air.type);

                _bw.Write(buildTicks);

                _bw.Write(lastMissingItem != null);
                if (lastMissingItem != null)
                    _bw.Write(lastMissingItem);

                break;
        }
    }

    //public void EnableBoundHelper(float progress = 0)
    //{
    //    if (BoundsHelper == null) return;
    //    BoundsHelper.localPosition = buildPosition.ToVector3() -
    //        Origin.position + new Vector3(0.5f, 0.5f, 0.5f);
    //    BoundsHelper.gameObject.SetActive(this.isOn);
    //    Color color = Color.yellow * (1f - progress) + Color.green * progress;
    //    if (lastColor == color) return;
    //    foreach (Renderer componentsInChild in BoundsHelper.GetComponentsInChildren<Renderer>())
    //        componentsInChild.material.SetColor("_Color", color * 0.5f);
    //    lastColor = color;
    //}

    //private Color lastColor = Color.clear;

    //public void ResetBoundHelper(Color color)
    //{
    //    if (BoundsHelper == null) return;
    //    BoundsHelper.localPosition = ToWorldPos().ToVector3() -
    //        Origin.position + new Vector3(0.5f, 0.5f, 0.5f);
    //    BoundsHelper.gameObject.SetActive(this.isOn);
    //    // Only update if necessary
    //    if (lastColor == color) return;
    //    foreach (Renderer componentsInChild in BoundsHelper.GetComponentsInChildren<Renderer>())
    //        componentsInChild.material.SetColor("_Color", color * 0.5f);
    //    lastColor = color;
    //}
}