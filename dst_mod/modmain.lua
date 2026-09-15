-- Para's Trainer — client-side admin cheat mod for a self-hosted DST world.
--
-- Two ways to use it:
--   1. In-game hotkeys (F1-F7) — work standalone, no external program needed.
--   2. Live bridge to ParasTrainer — the mod polls a command file the trainer
--      writes and executes the commands on the running game, and writes a status
--      file back. This lets the trainer drive an ALREADY-RUNNING game.
--
-- Everything fires the game's own built-in admin console commands, so it only
-- works when you are the server admin (hosting your own world).
--
-- IPC note: DST hard-sandboxes `io` for mods (io.open throws "invalid filepath"
-- for ANY path), so the bridge uses Klei's sanctioned persistent-string API
-- instead. Files land in the client persistent root:
--   ...\Klei\DoNotStarveTogether\<account>\client_save\
-- The trainer writes "command.txt" there and reads "status.txt" from there.

local _G = GLOBAL
local pcall = _G.pcall   -- mod-env whitelist omits pcall/tonumber/etc; reach via _G

local PS_CMD = "command.txt"       -- trainer -> mod (read via GetPersistentString)
local PS_STATUS = "status.txt"     -- mod -> trainer (written via SetPersistentString)

-- Tracked toggle state (source of truth for the toggles this mod manages).
local state = { god = false, freecraft = false, speed = false }

-- ── Command execution ──────────────────────────────────────────────────────

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
        Run("c_godmode()")
        state.god = false
    else
        Run("c_sethealth(1) c_setsanity(1) c_sethunger(1)")
    end
end

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

-- ── Live bridge (persistent-string IPC) ─────────────────────────────────────

local lastSeq = nil     -- highest command seq seen; nil until first read (no replay)

local function ProcessCommands(data)
    local first = (lastSeq == nil)
    local maxseq = lastSeq or 0
    for line in data:gmatch("[^\r\n]+") do
        local s, tok = line:match("^(%d+)|(.+)$")
        s = _G.tonumber(s)
        if s then
            if (not first) and s > (lastSeq or 0) then
                pcall(function() Handle(tok) end)
            end
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
        if ok and data ~= nil and data ~= "" then
            pcall(function() ProcessCommands(data) end)
        end
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
    local s = "CoreVersion=dst-1\n"
        .. "InGame=" .. (p ~= nil and 1 or 0) .. "\n"
        .. "Admin=" .. admin .. "\n"
        .. "AckSeq=" .. _G.tostring(lastSeq or 0) .. "\n"
        .. "Health=" .. h .. "\n"
        .. "Hunger=" .. hu .. "\n"
        .. "Sanity=" .. sa .. "\n"
        .. "GodMode=" .. (state.god and 1 or 0) .. "\n"
        .. "FreeCraft=" .. (state.freecraft and 1 or 0) .. "\n"
        .. "Speed=" .. (state.speed and 1 or 0) .. "\n"
    _G.TheSim:SetPersistentString(PS_STATUS, s, false, function() end)
end

local statusCounter = 0
local function Tick()
    pcall(ReadCommands)
    statusCounter = statusCounter + 1
    if statusCounter >= 3 then       -- status ~every 0.9s; commands polled every 0.3s
        statusCounter = 0
        pcall(WriteStatus)
    end
end

local started = false
local function StartBridge(src)
    if started then return end
    if _G.staticScheduler == nil then return end
    started = true
    _G.staticScheduler:ExecutePeriodic(0.3, Tick, nil, 0, "paras_trainer_ipc")
    _G.print("[Para Trainer] IPC bridge active (" .. _G.tostring(src) .. ") via persistent-string")
end

StartBridge("load")
AddGamePostInit(function() StartBridge("gamepostinit") end)
AddSimPostInit(function() StartBridge("simpostinit") end)

_G.print("[Para Trainer] loaded — F1 Restore, F2 God, F3 FreeCraft, F4 Refill, F5 Comfort, F6 Speed, F7 Help")
