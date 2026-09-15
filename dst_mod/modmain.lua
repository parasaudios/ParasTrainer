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
-- `party` = apply cheats to ALL players (not just the casting admin).
local state = { god = false, freecraft = false, speed = false, party = false, nospoil = false, infdura = false }

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

-- Server-side per-player operations (var 'p'). Applied to the caster (solo) or
-- every player (party-wide) via Apply(). Same component methods the c_ commands
-- use internally, so solo behaviour matches the built-in cheats.
local SNIP_GOD_ON    = [[if p.components.health then p.components.health:SetInvincible(true) end]]
local SNIP_GOD_OFF   = [[if p.components.health then p.components.health:SetInvincible(false) end]]
local SNIP_REFILL    = [[if not p:HasTag("playerghost") then if p.components.health then p.components.health:SetPercent(1) end if p.components.sanity then p.components.sanity:SetPercent(1) end if p.components.hunger then p.components.hunger:SetPercent(1) end end]]
local SNIP_COMFORT   = [[if p.components.moisture then p.components.moisture:SetPercent(0) end if p.components.temperature then p.components.temperature:SetTemperature(25) end]]
-- Free Crafting: set the SERVER field directly (idempotent) — GiveAllRecipes()
-- TOGGLES freebuildmode, which is wrong in a repeating maintenance loop. Setting
-- the field cascades to the guest's replica netvar, enabling their crafting UI.
local SNIP_FC_ON     = [[if p.components.builder and not p.components.builder.freebuildmode then p.components.builder.freebuildmode = true p:PushEvent("techlevelchange") end]]
local SNIP_FC_OFF    = [[if p.components.builder and p.components.builder.freebuildmode then p.components.builder.freebuildmode = false p:PushEvent("techlevelchange") end]]
local SNIP_SPEED_ON  = [[if p.components.locomotor then p.components.locomotor:SetExternalSpeedMultiplier(p,"paras_trainer",2) end]]
local SNIP_SPEED_OFF = [[if p.components.locomotor then p.components.locomotor:RemoveExternalSpeedMultiplier(p,"paras_trainer") end]]
local SNIP_RESTORE   = [[if p.components.health then p.components.health:SetInvincible(true) if not p:HasTag("playerghost") then p.components.health:SetPercent(1) end end if p.components.sanity then p.components.sanity:SetPercent(1) end if p.components.hunger then p.components.hunger:SetPercent(1) end if p.components.moisture then p.components.moisture:SetPercent(0) end if p.components.temperature then p.components.temperature:SetTemperature(25) end]]
-- Heal a living player fully, or bring a ghost back to life.
local SNIP_REVIVE    = [[if p:HasTag("playerghost") then p:PushEvent("respawnfromghost") else if p.components.health then p.components.health:SetPercent(1) end if p.components.sanity then p.components.sanity:SetPercent(1) end if p.components.hunger then p.components.hunger:SetPercent(1) end end]]
-- Item maintenance (over inventory + equipped + backpack via ForEachItem).
local SNIP_NOSPOIL_ON  = [[if p.components.inventory then p.components.inventory:ForEachItem(function(it) if it and it.components.perishable then it.components.perishable:SetLocalMultiplier(0) end end) end]]
local SNIP_NOSPOIL_OFF = [[if p.components.inventory then p.components.inventory:ForEachItem(function(it) if it and it.components.perishable then it.components.perishable:SetLocalMultiplier(1) end end) end]]
local SNIP_INFDURA     = [[if p.components.inventory then p.components.inventory:ForEachItem(function(it) if it then local fu=it.components.finiteuses if fu and fu.current<fu.total then fu:SetUses(fu.total) end local ar=it.components.armor if ar and not ar.indestructible and ar.condition<ar.maxcondition then ar:SetPercent(1) end local fl=it.components.fueled if fl and not fl:IsFull() then fl:SetPercent(1) end end end) end]]

-- Apply a per-player snippet to the caster (solo) or all players (party-wide).
-- The AllPlayers loop is guarded to player entities only — it never touches mobs.
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

local function Handle(token)
    -- Scope toggle (doesn't require being in-game).
    if token == "party:on" then state.party = true; Notify("Party-wide: ON (cheats affect ALL players)"); return end
    if token == "party:off" then state.party = false; Notify("Party-wide: OFF (just you)"); return end

    -- Targeted heal/revive for a specific player: "revive:<userid>".
    if token:sub(1, 7) == "revive:" then
        if not CanCheat() then Notify("needs server admin"); return end
        ApplyTo(token:sub(8), SNIP_REVIVE)
        Notify("Heal / Revive sent to player")
        return
    end

    if not CanCheat() then
        Notify("needs server admin — host your own world")
        return
    end
    local who = state.party and "everyone" or "you"
    if token == "restore" then
        Apply(SNIP_RESTORE); state.god = true; Notify("Full Restore for " .. who)
    elseif token == "god:on" then
        Apply(SNIP_GOD_ON); state.god = true; Notify("God Mode ON (" .. who .. ")")
    elseif token == "god:off" then
        Apply(SNIP_GOD_OFF); state.god = false; Notify("God Mode OFF (" .. who .. ")")
    elseif token == "god:toggle" then
        Handle(state.god and "god:off" or "god:on")
    elseif token == "freecraft:on" then
        Apply(SNIP_FC_ON); state.freecraft = true; Notify("Free Crafting ON (" .. who .. ")")
    elseif token == "freecraft:off" then
        Apply(SNIP_FC_OFF); state.freecraft = false; Notify("Free Crafting OFF (" .. who .. ")")
    elseif token == "freecraft:toggle" then
        Handle(state.freecraft and "freecraft:off" or "freecraft:on")
    elseif token == "refill" then
        Apply(SNIP_REFILL); Notify("Refilled " .. who)
    elseif token == "comfort" then
        Apply(SNIP_COMFORT); Notify("Comfort for " .. who)
    elseif token == "speed:on" then
        Apply(SNIP_SPEED_ON); state.speed = true; Notify("Move Speed x2 ON (" .. who .. ")")
    elseif token == "speed:off" then
        Apply(SNIP_SPEED_OFF); state.speed = false; Notify("Move Speed x2 OFF (" .. who .. ")")
    elseif token == "speed:toggle" then
        Handle(state.speed and "speed:off" or "speed:on")
    elseif token == "revive" then
        Apply(SNIP_REVIVE); Notify("Revive / Heal for " .. who)
    elseif token == "nospoil:on" then
        Apply(SNIP_NOSPOIL_ON); state.nospoil = true; Notify("No Spoil ON (" .. who .. ")")
    elseif token == "nospoil:off" then
        Apply(SNIP_NOSPOIL_OFF); state.nospoil = false; Notify("No Spoil OFF (" .. who .. ")")
    elseif token == "nospoil:toggle" then
        Handle(state.nospoil and "nospoil:off" or "nospoil:on")
    elseif token == "infdura:on" then
        Apply(SNIP_INFDURA); state.infdura = true; Notify("Infinite Durability ON (" .. who .. ")")
    elseif token == "infdura:off" then
        state.infdura = false; Notify("Infinite Durability OFF (" .. who .. ")")
    elseif token == "infdura:toggle" then
        Handle(state.infdura and "infdura:off" or "infdura:on")
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
    -- Current players (userid~name;...) for the trainer's per-player heal/revive.
    -- Names are sanitised so they can't break the key=val;delimited format.
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
        .. "Party=" .. (state.party and 1 or 0) .. "\n"
        .. "NoSpoil=" .. (state.nospoil and 1 or 0) .. "\n"
        .. "InfDura=" .. (state.infdura and 1 or 0) .. "\n"
        .. "Players=" .. players .. "\n"
    _G.TheSim:SetPersistentString(PS_STATUS, s, false, function() end)
end

-- Re-assert active toggles + run item maintenance every ~2s. This keeps state
-- sticky for players who JOIN after a cheat was enabled (party-wide re-applies
-- to everyone present now) and keeps No Spoil / Infinite Durability covering
-- newly acquired items. Guards avoid event spam (only act when not already set).
local function Maintain()
    if not CanCheat() then return end
    local ops = ""
    if state.god then ops = ops .. [[if p.components.health and not p.components.health.invincible then p.components.health:SetInvincible(true) end ]] end
    if state.freecraft then ops = ops .. SNIP_FC_ON .. " " end
    if state.speed then ops = ops .. SNIP_SPEED_ON .. " " end
    if state.nospoil then ops = ops .. SNIP_NOSPOIL_ON .. " " end
    if state.infdura then ops = ops .. SNIP_INFDURA .. " " end
    if ops ~= "" then Apply(ops) end
end

local statusCounter = 0
local maintCounter = 0
local function Tick()
    pcall(ReadCommands)
    statusCounter = statusCounter + 1
    if statusCounter >= 3 then       -- status ~every 0.9s; commands polled every 0.3s
        statusCounter = 0
        pcall(WriteStatus)
    end
    maintCounter = maintCounter + 1
    if maintCounter >= 6 then         -- maintenance ~every 1.8s
        maintCounter = 0
        pcall(Maintain)
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
