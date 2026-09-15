-- Para's Trainer — client-side admin cheat hotkeys for a self-hosted DST world.
-- Fires the game's own built-in console cheats via admin remote-execute, so no
-- server-side mod is needed. Only works when you are the server host/admin.
--
-- Design notes (verified against the game's consolecommands.lua):
--  * c_godmode / c_supergodmode / c_freecrafting self-route to the server when
--    typed on a client. We send them with SendRemoteExecute, so they arrive on
--    the master sim already and run directly (no double-hop).
--  * c_sethealth / c_setsanity / c_sethunger / c_setmoisture / c_settemperature
--    do NOT self-route and write directly to components, which are read-only
--    replicas on a client. They ONLY work when run on the server, so we always
--    send them through SendRemoteExecute too.
--  * All of these resolve the target via ConsoleCommandPlayer(), which on the
--    server resolves to the requesting admin (same path c_supergodmode relies on).

local _G = GLOBAL
local TheInput = _G.TheInput
local TheNet = _G.TheNet

local KEYS = {
    SUPERGOD  = _G.KEY_F1,
    GOD       = _G.KEY_F2,
    FREECRAFT = _G.KEY_F3,
    REFILL    = _G.KEY_F4,
    COMFORT   = _G.KEY_F5,
    SPEED     = _G.KEY_F6,
    HELP      = _G.KEY_F7,
}

local function Player() return _G.ThePlayer end

local function IsAdmin() return TheNet ~= nil and TheNet:GetIsServerAdmin() end

-- Transient on-screen feedback via the character's speech bubble + console log.
-- The client talker replica may not accept Say, so guard it; print always works.
local function Notify(msg)
    local p = Player()
    if p ~= nil and p.components ~= nil and p.components.talker ~= nil then
        pcall(function() p.components.talker:Say("[Trainer] " .. msg, 2) end)
    end
    print("[Para Trainer] " .. msg)
end

-- Run a console command string on the server, targeting the calling admin.
local function Remote(cmd)
    TheNet:SendRemoteExecute(cmd)
end

local function Bind(key, fn)
    if key == nil then return end
    TheInput:AddKeyDownHandler(key, function()
        if Player() == nil then return end          -- not in-game yet
        if not IsAdmin() then
            Notify("needs server admin — host your own world")
            return
        end
        local ok, err = pcall(fn)
        if not ok then print("[Para Trainer] error: " .. tostring(err)) end
    end)
end

-- F1  Super God Mode: invincible + full health/sanity/hunger + comfy temp/moisture.
Bind(KEYS.SUPERGOD, function()
    Remote("c_supergodmode()")
    Notify("Super God Mode toggled")
end)

-- F2  Plain God Mode (invincibility toggle; also revives if you're a ghost).
Bind(KEYS.GOD, function()
    Remote("c_godmode()")
    Notify("God Mode toggled")
end)

-- F3  Free Crafting (all recipes, no ingredients/prototypes needed).
Bind(KEYS.FREECRAFT, function()
    Remote("c_freecrafting()")
    Notify("Free Crafting toggled")
end)

-- F4  Refill health / sanity / hunger to full (without touching god mode).
Bind(KEYS.REFILL, function()
    Remote("c_sethealth(1) c_setsanity(1) c_sethunger(1)")
    Notify("Health / Sanity / Hunger refilled")
end)

-- F5  Dry off + comfortable temperature.
Bind(KEYS.COMFORT, function()
    Remote("c_setmoisture(0) c_settemperature(25)")
    Notify("Dried off + comfortable temperature")
end)

-- F6  Move Speed x2 toggle. No built-in command, so run a small snippet on the
-- server that flips an external speed multiplier on the calling admin's player.
Bind(KEYS.SPEED, function()
    Remote([[
        local p = ConsoleCommandPlayer()
        if p ~= nil and p.components ~= nil and p.components.locomotor ~= nil then
            if p._ptrainer_fast then
                p.components.locomotor:RemoveExternalSpeedMultiplier(p, "paras_trainer")
                p._ptrainer_fast = nil
            else
                p.components.locomotor:SetExternalSpeedMultiplier(p, "paras_trainer", 2)
                p._ptrainer_fast = true
            end
        end
    ]])
    Notify("Move Speed x2 toggled")
end)

-- F7  Help.
Bind(KEYS.HELP, function()
    Notify("F1 SuperGod  F2 God  F3 FreeCraft  F4 Refill  F5 Comfort  F6 Speed")
end)

print("[Para Trainer] loaded — F1 SuperGod, F2 God, F3 FreeCraft, F4 Refill, F5 Comfort, F6 Speed, F7 Help")
