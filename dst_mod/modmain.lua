-- Para's Trainer — LOADER (stable). Hot-swappable cheat logic lives in a separate
-- file, paras_logic.lua, in the client persistent root (client_save). This loader
-- reads it via GetPersistentString, loadstring()s it, and HOT-RELOADS it whenever
-- its contents change (~every 2s). So cheat logic can be updated live with no game
-- restart. If a new logic file fails to compile/run, the last good one is kept.
--
-- The loader itself only owns stable plumbing (IPC bridge, status, scheduler,
-- hotkeys, and a small helper API passed to the logic as `ctx`). It should rarely
-- need to change; iterate in paras_logic.lua instead.
--
-- IPC (see history): DST sandboxes io for mods (throws), and os.getenv is absent,
-- so everything goes through Klei's persistent-string API in client_save.

local _G = GLOBAL
local pcall = _G.pcall

local PS_CMD = "command.txt"        -- trainer -> mod (tokens)
local PS_STATUS = "status.txt"      -- mod -> trainer (stats + toggle states + roster)
local PS_LOGIC = "paras_logic.lua"  -- hot-swappable cheat logic (I edit this live)

-- Shared toggle state. The hot logic mutates this table; the loader's status
-- writer reads it. Passed by reference via ctx, so both see the same values.
local state = { god = false, freecraft = false, speed = false, party = false, nospoil = false, infdura = false }

-- ── Stable helper API (handed to the hot logic as ctx) ──────────────────────

-- Run a console command on the authoritative sim.
local function Run(cmd)
    if _G.TheWorld ~= nil and _G.TheWorld.ismastersim then
        _G.ExecuteConsoleCommand(cmd)
    else
        _G.TheNet:SendRemoteExecute(cmd)
    end
end

local function CanCheat()
    return _G.ThePlayer ~= nil and _G.TheNet ~= nil and _G.TheNet:GetIsServerAdmin()
end

local function Notify(msg)
    local p = _G.ThePlayer
    if p ~= nil and p.components ~= nil and p.components.talker ~= nil then
        pcall(function() p.components.talker:Say("[Trainer] " .. msg, 2) end)
    end
    _G.print("[Para Trainer] " .. msg)
end

-- Apply a per-player snippet (var 'p') to the caster (solo) or all players
-- (party-wide). AllPlayers loop is guarded to player entities only.
local function Apply(perPlayer)
    if state.party then
        Run("for k,p in ipairs(AllPlayers) do if p and p:HasTag(\"player\") then " .. perPlayer .. " end end")
    else
        Run("local p = ConsoleCommandPlayer() if p then " .. perPlayer .. " end")
    end
end

-- Apply a per-player snippet to one specific player, by userid.
local function ApplyTo(userid, perPlayer)
    Run("local p = UserToPlayer(\"" .. userid .. "\") if p then " .. perPlayer .. " end")
end

local ctx = {
    state = state,
    Run = Run,
    Apply = Apply,
    ApplyTo = ApplyTo,
    Notify = Notify,
    CanCheat = CanCheat,
    G = _G,
}

-- ── Hot logic loading ───────────────────────────────────────────────────────

local logic = nil          -- { version, handle(token), maintain() }
local lastLogicSrc = nil

local function ReloadLogic(src)
    if src == nil or src == "" or src == lastLogicSrc then return end
    local fn, err = _G.loadstring(src, "paras_logic")
    if not fn then
        _G.print("[Para Trainer] logic compile error: " .. _G.tostring(err))
        return
    end
    if _G.setfenv then _G.setfenv(fn, _G) end
    local ok, api = pcall(fn, ctx)
    if ok and type(api) == "table" and api.handle ~= nil then
        logic = api
        lastLogicSrc = src
        _G.print("[Para Trainer] hot-loaded paras_logic.lua v" .. _G.tostring(api.version or "?"))
        Notify("logic updated -> v" .. _G.tostring(api.version or "?"))
    else
        _G.print("[Para Trainer] logic load failed (kept previous): " .. _G.tostring(api))
    end
end

local logicReading = false
local function PollLogic()
    if logicReading then return end
    logicReading = true
    _G.TheSim:GetPersistentString(PS_LOGIC, function(ok, data)
        logicReading = false
        if ok and data ~= nil and data ~= "" then pcall(function() ReloadLogic(data) end) end
    end)
end

local function DoHandle(token)
    if logic ~= nil and logic.handle ~= nil then
        local ok, err = pcall(logic.handle, token)
        if not ok then _G.print("[Para Trainer] handle error: " .. _G.tostring(err)) end
    else
        Notify("cheat logic not loaded yet (paras_logic.lua)")
    end
end

local function DoMaintain()
    if logic ~= nil and logic.maintain ~= nil then pcall(logic.maintain) end
end

-- ── Hotkeys (standalone; delegate to the hot logic) ─────────────────────────

local function Bind(key, token)
    if key == nil then return end
    _G.TheInput:AddKeyDownHandler(key, function()
        if _G.ThePlayer == nil then return end
        DoHandle(token)
    end)
end

Bind(_G.KEY_F1, "restore")
Bind(_G.KEY_F2, "god:toggle")
Bind(_G.KEY_F3, "freecraft:toggle")
Bind(_G.KEY_F4, "refill")
Bind(_G.KEY_F5, "comfort")
Bind(_G.KEY_F6, "speed:toggle")
_G.TheInput:AddKeyDownHandler(_G.KEY_F7, function()
    if _G.ThePlayer ~= nil then
        Notify("F1 Restore  F2 God  F3 FreeCraft  F4 Refill  F5 Comfort  F6 Speed")
    end
end)

-- ── Command bridge (tokens; interpreted by the hot logic) ───────────────────

local lastSeq = nil
local function ProcessCommands(data)
    local first = (lastSeq == nil)
    local maxseq = lastSeq or 0
    for line in data:gmatch("[^\r\n]+") do
        local s, tok = line:match("^(%d+)|(.+)$")
        s = _G.tonumber(s)
        if s then
            if (not first) and s > (lastSeq or 0) then DoHandle(tok) end
            if s > maxseq then maxseq = s end
        end
    end
    lastSeq = maxseq
end

local reading = false
local function ReadCommands()
    if reading then return end
    reading = true
    _G.TheSim:GetPersistentString(PS_CMD, function(ok, data)
        reading = false
        if ok and data ~= nil and data ~= "" then pcall(function() ProcessCommands(data) end) end
    end)
end

local function Pct(replica)
    if replica ~= nil and replica.GetPercent ~= nil then
        local ok, v = pcall(function() return replica:GetPercent() end)
        if ok and v ~= nil then return _G.math.floor(v * 100 + 0.5) end
    end
    return -1
end

local function WriteStatus()
    local p = _G.ThePlayer
    local admin = (_G.TheNet ~= nil and _G.TheNet:GetIsServerAdmin()) and 1 or 0
    local h, hu, sa = -1, -1, -1
    if p ~= nil and p.replica ~= nil then
        h = Pct(p.replica.health)
        hu = Pct(p.replica.hunger)
        sa = Pct(p.replica.sanity)
    end
    local players = ""
    if _G.TheNet ~= nil and _G.TheNet.GetClientTable ~= nil then
        local ok, ct = pcall(function() return _G.TheNet:GetClientTable() end)
        if ok and ct ~= nil then
            for i, cl in _G.ipairs(ct) do
                local uid = cl.userid or ""
                local nm = (cl.name or "?"):gsub("[=;~\r\n]", " ")
                if uid ~= "" then players = players .. uid .. "~" .. nm .. ";" end
            end
        end
    end
    local s = "CoreVersion=" .. (logic ~= nil and ("logic-" .. _G.tostring(logic.version or "?")) or "loader-2.0") .. "\n"
        .. "InGame=" .. (p ~= nil and 1 or 0) .. "\n"
        .. "Admin=" .. admin .. "\n"
        .. "AckSeq=" .. _G.tostring(lastSeq or 0) .. "\n"
        .. "Health=" .. h .. "\n"
        .. "Hunger=" .. hu .. "\n"
        .. "Sanity=" .. sa .. "\n"
        .. "GodMode=" .. (state.god and 1 or 0) .. "\n"
        .. "FreeCraft=" .. (state.freecraft and 1 or 0) .. "\n"
        .. "Speed=" .. (state.speed and 1 or 0) .. "\n"
        .. "NoSpoil=" .. (state.nospoil and 1 or 0) .. "\n"
        .. "InfDura=" .. (state.infdura and 1 or 0) .. "\n"
        .. "Party=" .. (state.party and 1 or 0) .. "\n"
        .. "Players=" .. players .. "\n"
    _G.TheSim:SetPersistentString(PS_STATUS, s, false, function() end)
end

local tickN = 0
local function Tick()
    pcall(ReadCommands)
    tickN = tickN + 1
    if tickN % 3 == 0 then pcall(WriteStatus) end      -- status ~every 0.9s
    if tickN % 6 == 0 then pcall(DoMaintain) end        -- maintenance ~every 1.8s
    if tickN % 7 == 0 then pcall(PollLogic) end         -- hot-reload check ~every 2.1s
end

local started = false
local function StartBridge(src)
    if started then return end
    if _G.staticScheduler == nil then return end
    started = true
    pcall(PollLogic)   -- load logic ASAP
    _G.staticScheduler:ExecutePeriodic(0.3, Tick, nil, 0, "paras_trainer_ipc")
    _G.print("[Para Trainer] loader active (" .. _G.tostring(src) .. ") — hot logic from paras_logic.lua")
end

StartBridge("load")
AddGamePostInit(function() StartBridge("gamepostinit") end)
AddSimPostInit(function() StartBridge("simpostinit") end)

_G.print("[Para Trainer] LOADER 2.0 loaded — hotkeys F1-F7; cheat logic hot-swaps from paras_logic.lua")
