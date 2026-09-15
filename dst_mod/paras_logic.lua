-- Para's Trainer — HOT cheat logic. The loader (modmain.lua) reads this file from
-- client_save, loadstring()s it, and hot-reloads it whenever it changes (~2s).
-- EDIT THIS FILE LIVE — no game or trainer restart needed. Bump `version` on each
-- edit so you can see the reload confirmed in-game ("logic updated -> vX").
--
-- Runs with the game globals as its environment. ctx (passed as ...) provides the
-- stable helper API from the loader.

local ctx = ...
local state    = ctx.state       -- shared toggle state (loader's status reads it)
local Apply    = ctx.Apply       -- Apply(perPlayerSnippet): scope-aware (caster or all)
local ApplyTo  = ctx.ApplyTo     -- ApplyTo(userid, perPlayerSnippet): one player
local Notify   = ctx.Notify
local CanCheat = ctx.CanCheat

-- Server-side per-player operations (var 'p').
local SNIP_GOD_ON    = [[if p.components.health then p.components.health:SetInvincible(true) end]]
local SNIP_GOD_OFF   = [[if p.components.health then p.components.health:SetInvincible(false) end]]
local SNIP_REFILL    = [[if not p:HasTag("playerghost") then if p.components.health then p.components.health:SetPercent(1) end if p.components.sanity then p.components.sanity:SetPercent(1) end if p.components.hunger then p.components.hunger:SetPercent(1) end end]]
local SNIP_COMFORT   = [[if p.components.moisture then p.components.moisture:SetPercent(0) end if p.components.temperature then p.components.temperature:SetTemperature(25) end]]
-- Free crafting: set the SERVER field directly (idempotent). GiveAllRecipes TOGGLES.
local SNIP_FC_ON     = [[if p.components.builder and not p.components.builder.freebuildmode then p.components.builder.freebuildmode = true p:PushEvent("techlevelchange") end]]
local SNIP_FC_OFF    = [[if p.components.builder and p.components.builder.freebuildmode then p.components.builder.freebuildmode = false p:PushEvent("techlevelchange") end]]
local SNIP_SPEED_ON  = [[if p.components.locomotor then p.components.locomotor:SetExternalSpeedMultiplier(p,"paras_trainer",2) end]]
local SNIP_SPEED_OFF = [[if p.components.locomotor then p.components.locomotor:RemoveExternalSpeedMultiplier(p,"paras_trainer") end]]
local SNIP_RESTORE   = [[if p.components.health then p.components.health:SetInvincible(true) if not p:HasTag("playerghost") then p.components.health:SetPercent(1) end end if p.components.sanity then p.components.sanity:SetPercent(1) end if p.components.hunger then p.components.hunger:SetPercent(1) end if p.components.moisture then p.components.moisture:SetPercent(0) end if p.components.temperature then p.components.temperature:SetTemperature(25) end]]
local SNIP_REVIVE    = [[if p:HasTag("playerghost") then p:PushEvent("respawnfromghost") else if p.components.health then p.components.health:SetPercent(1) end if p.components.sanity then p.components.sanity:SetPercent(1) end if p.components.hunger then p.components.hunger:SetPercent(1) end end]]
local SNIP_NOSPOIL_ON  = [[if p.components.inventory then p.components.inventory:ForEachItem(function(it) if it and it.components.perishable then it.components.perishable:SetLocalMultiplier(0) end end) end]]
local SNIP_NOSPOIL_OFF = [[if p.components.inventory then p.components.inventory:ForEachItem(function(it) if it and it.components.perishable then it.components.perishable:SetLocalMultiplier(1) end end) end]]
local SNIP_INFDURA     = [[if p.components.inventory then p.components.inventory:ForEachItem(function(it) if it then local fu=it.components.finiteuses if fu and fu.current<fu.total then fu:SetUses(fu.total) end local ar=it.components.armor if ar and not ar.indestructible and ar.condition<ar.maxcondition then ar:SetPercent(1) end local fl=it.components.fueled if fl and not fl:IsFull() then fl:SetPercent(1) end end end) end]]
local SNIP_GOD_KEEP  = [[if p.components.health and not p.components.health.invincible then p.components.health:SetInvincible(true) end]]

local function handle(token)
    if token == "party:on" then state.party = true; Notify("Party-wide: ON (ALL players)"); return end
    if token == "party:off" then state.party = false; Notify("Party-wide: OFF (just you)"); return end

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
        handle(state.god and "god:off" or "god:on")
    elseif token == "freecraft:on" then
        Apply(SNIP_FC_ON); state.freecraft = true; Notify("Free Crafting ON (" .. who .. ")")
    elseif token == "freecraft:off" then
        Apply(SNIP_FC_OFF); state.freecraft = false; Notify("Free Crafting OFF (" .. who .. ")")
    elseif token == "freecraft:toggle" then
        handle(state.freecraft and "freecraft:off" or "freecraft:on")
    elseif token == "refill" then
        Apply(SNIP_REFILL); Notify("Refilled " .. who)
    elseif token == "comfort" then
        Apply(SNIP_COMFORT); Notify("Comfort for " .. who)
    elseif token == "speed:on" then
        Apply(SNIP_SPEED_ON); state.speed = true; Notify("Move Speed x2 ON (" .. who .. ")")
    elseif token == "speed:off" then
        Apply(SNIP_SPEED_OFF); state.speed = false; Notify("Move Speed x2 OFF (" .. who .. ")")
    elseif token == "speed:toggle" then
        handle(state.speed and "speed:off" or "speed:on")
    elseif token == "revive" then
        Apply(SNIP_REVIVE); Notify("Revive / Heal for " .. who)
    elseif token == "nospoil:on" then
        Apply(SNIP_NOSPOIL_ON); state.nospoil = true; Notify("No Spoil ON (" .. who .. ")")
    elseif token == "nospoil:off" then
        Apply(SNIP_NOSPOIL_OFF); state.nospoil = false; Notify("No Spoil OFF (" .. who .. ")")
    elseif token == "nospoil:toggle" then
        handle(state.nospoil and "nospoil:off" or "nospoil:on")
    elseif token == "infdura:on" then
        Apply(SNIP_INFDURA); state.infdura = true; Notify("Infinite Durability ON (" .. who .. ")")
    elseif token == "infdura:off" then
        state.infdura = false; Notify("Infinite Durability OFF (" .. who .. ")")
    elseif token == "infdura:toggle" then
        handle(state.infdura and "infdura:off" or "infdura:on")
    end
end

-- Re-assert active toggles + run item maintenance. Keeps state sticky for late
-- joiners (party-wide) and keeps No Spoil / Infinite Durability covering new items.
local function maintain()
    if not CanCheat() then return end
    local ops = ""
    if state.god then ops = ops .. SNIP_GOD_KEEP .. " " end
    if state.freecraft then ops = ops .. SNIP_FC_ON .. " " end
    if state.speed then ops = ops .. SNIP_SPEED_ON .. " " end
    if state.nospoil then ops = ops .. SNIP_NOSPOIL_ON .. " " end
    if state.infdura then ops = ops .. SNIP_INFDURA .. " " end
    if ops ~= "" then Apply(ops) end
end

return { version = "2.0.0", handle = handle, maintain = maintain }
