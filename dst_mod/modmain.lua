-- Para's Trainer — client-side admin cheat mod for a self-hosted DST world.
--
-- Two ways to use it:
--   1. In-game hotkeys (F1-F7) — work standalone, no external program needed.
--   2. Live bridge to ParasTrainer — the mod polls a command file the trainer
--      writes and executes the commands on the running game, and writes a status
--      file back. This is what lets the trainer drive an ALREADY-RUNNING game.
--
-- Everything fires the game's own built-in admin console commands, so it only
-- works when you are the server admin (hosting your own world).
--
-- IPC path note: DST's Lua `os` table has NO os.getenv, so we can't read %TEMP%.
-- Instead we use a FIXED absolute path next to the mod (local disk, persistent,
-- easy to inspect). ParasTrainer's DSTPlugin points at the exact same folder.

local _G = GLOBAL
local io = _G.io

-- ── IPC paths (must match DSTPlugin.IpcDirectory in ParasTrainer) ──
local IPC = "J:\\SteamLibrary\\steamapps\\common\\Don't Starve Together\\mods\\paras_trainer\\ipc\\"
local CMD_FILE = IPC .. "command.txt"
local STATUS_FILE = IPC .. "status.txt"

-- Tracked toggle state (source of truth for the toggles this mod manages).
local state = { god = false, freecraft = false, speed = false }

-- ── Command execution ──────────────────────────────────────────────────────

-- Run a console command on the authoritative sim: locally if we ARE the server
-- (listen-server host), else send to the server as an admin (client + dedicated
-- server, which is the usual self-host setup).
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

local function SetSpeed(on)
    if on == state.speed then return end
    state.speed = on
    if on then
        Run([[local p=ConsoleCommandPlayer() if p and p.components and p.components.locomotor then p.components.locomotor:SetExternalSpeedMultiplier(p,"paras_trainer",2) end]])
    else
        Run([[local p=ConsoleCommandPlayer() if p and p.components and p.components.locomotor then p.components.locomotor:RemoveExternalSpeedMultiplier(p,"paras_trainer") end]])
    end
end

local function DoRevive()
    local p = _G.ThePlayer
    if p ~= nil and p.HasTag and p:HasTag("playerghost") then
        Run("c_godmode()")   -- c_godmode on a ghost => respawnfromghost
        state.god = false
    else
        Run("c_sethealth(1) c_setsanity(1) c_sethunger(1)")
    end
end

-- Semantic command vocabulary (used by both the hotkeys and the trainer bridge).
local function Handle(token)
    if not CanCheat() then
        Notify("needs server admin — host your own world")
        return
    end
    if token == "restore" then
        if not state.god then Run("c_godmode()"); state.god = true end
        Run("c_sethealth(1) c_setsanity(1) c_sethunger(1) c_setmoisture(0) c_settemperature(25)")
        Notify("Full Restore (God Mode on, stats full, comfy)")
    elseif token == "god:on" then
        if not state.god then Run("c_godmode()"); state.god = true end
        Notify("God Mode ON")
    elseif token == "god:off" then
        if state.god then Run("c_godmode()"); state.god = false end
        Notify("God Mode OFF")
    elseif token == "god:toggle" then
        Handle(state.god and "god:off" or "god:on")
    elseif token == "freecraft:on" then
        if not state.freecraft then Run("c_freecrafting()"); state.freecraft = true end
        Notify("Free Crafting ON")
    elseif token == "freecraft:off" then
        if state.freecraft then Run("c_freecrafting()"); state.freecraft = false end
        Notify("Free Crafting OFF")
    elseif token == "freecraft:toggle" then
        Handle(state.freecraft and "freecraft:off" or "freecraft:on")
    elseif token == "refill" then
        Run("c_sethealth(1) c_setsanity(1) c_sethunger(1)")
        Notify("Health / Sanity / Hunger refilled")
    elseif token == "comfort" then
        Run("c_setmoisture(0) c_settemperature(25)")
        Notify("Dried off + comfortable temperature")
    elseif token == "speed:on" then
        SetSpeed(true); Notify("Move Speed x2 ON")
    elseif token == "speed:off" then
        SetSpeed(false); Notify("Move Speed x2 OFF")
    elseif token == "speed:toggle" then
        SetSpeed(not state.speed); Notify("Move Speed x2 " .. (state.speed and "ON" or "OFF"))
    elseif token == "revive" then
        DoRevive(); Notify("Revive / Heal")
    end
end

-- ── Hotkeys (standalone; no trainer needed) ─────────────────────────────────

local function Bind(key, token)
    if key == nil then return end
    _G.TheInput:AddKeyDownHandler(key, function()
        if _G.ThePlayer == nil then return end
        local ok, err = pcall(function() Handle(token) end)
        if not ok then _G.print("[Para Trainer] hotkey error: " .. tostring(err)) end
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

-- ── Live bridge to ParasTrainer ─────────────────────────────────────────────

local lastSeq = nil     -- highest command seq seen; nil until first read (no replay)

local function ReadCommands()
    local f = io.open(CMD_FILE, "rb")
    if not f then return end
    local data = f:read("*a"); f:close()
    if not data then return end
    local first = (lastSeq == nil)
    local maxseq = lastSeq or 0
    for line in data:gmatch("[^\r\n]+") do
        local s, tok = line:match("^(%d+)|(.+)$")
        s = _G.tonumber(s)
        if s then
            if (not first) and s > (lastSeq or 0) then
                Handle(tok)
            end
            if s > maxseq then maxseq = s end
        end
    end
    lastSeq = maxseq
end

local function Pct(replica)
    if replica ~= nil and replica.GetPercent ~= nil then
        local ok, v = pcall(function() return replica:GetPercent() end)
        if ok and v ~= nil then return _G.math.floor(v * 100 + 0.5) end
    end
    return -1
end

local function WriteStatus()
    local f = io.open(STATUS_FILE, "wb")
    if not f then return end     -- ipc dir missing — skip (trainer recreates it)
    local p = _G.ThePlayer
    local admin = (_G.TheNet ~= nil and _G.TheNet:GetIsServerAdmin()) and 1 or 0
    local h, hu, sa = -1, -1, -1
    if p ~= nil and p.replica ~= nil then
        h = Pct(p.replica.health)
        hu = Pct(p.replica.hunger)
        sa = Pct(p.replica.sanity)
    end
    f:write("CoreVersion=dst-1\n")
    f:write("InGame=" .. (p ~= nil and 1 or 0) .. "\n")
    f:write("Admin=" .. admin .. "\n")
    f:write("AckSeq=" .. _G.tostring(lastSeq or 0) .. "\n")
    f:write("Health=" .. h .. "\n")
    f:write("Hunger=" .. hu .. "\n")
    f:write("Sanity=" .. sa .. "\n")
    f:write("GodMode=" .. (state.god and 1 or 0) .. "\n")
    f:write("FreeCraft=" .. (state.freecraft and 1 or 0) .. "\n")
    f:write("Speed=" .. (state.speed and 1 or 0) .. "\n")
    f:close()
end

local statusCounter = 0
local function Tick()
    pcall(ReadCommands)
    statusCounter = statusCounter + 1
    if statusCounter >= 3 then       -- write status ~every 0.9s (poll cmds every 0.3s)
        statusCounter = 0
        pcall(WriteStatus)
    end
end

-- Robust startup: try immediately and from both post-init hooks; only starts once.
local started = false
local function StartBridge(src)
    if started then return end
    if _G.staticScheduler == nil then return end
    started = true
    _G.staticScheduler:ExecutePeriodic(0.3, Tick, nil, 0, "paras_trainer_ipc")
    _G.print("[Para Trainer] IPC bridge active (" .. _G.tostring(src) .. ") -> " .. IPC)
end

_G.print("[Para Trainer] diag: io=" .. _G.tostring(_G.io)
    .. " staticScheduler=" .. _G.tostring(_G.staticScheduler)
    .. " IPC=" .. IPC)

StartBridge("load")
AddGamePostInit(function() StartBridge("gamepostinit") end)
AddSimPostInit(function() StartBridge("simpostinit") end)

_G.print("[Para Trainer] loaded — F1 Restore, F2 God, F3 FreeCraft, F4 Refill, F5 Comfort, F6 Speed, F7 Help")
