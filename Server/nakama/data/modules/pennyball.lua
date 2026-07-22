local nk = require("nakama")

-- Client WalletService / LeagueConfig ile aynı sabitler.
local REWARDS = {
  win  = { coins = 40, xp = 10 },
  draw = { coins = 20, xp = 10 },
  loss = { coins = 10, xp = 10 },
  forfeit = { coins = 0, xp = 0 }
}

local LEAGUE_POINTS = {
  win = 3,
  draw = 1,
  loss = 0,
  forfeit = 0
}

-- Single-node skor tutarlılığı + ödül idempotency.
local pending = {}

local function parse_payload(context, payload)
  if payload == nil or payload == "" then
    return nil
  end
  return nk.json_decode(payload)
end

local function normalize_result(result)
  if result == "win" or result == "draw" or result == "loss" or result == "forfeit" then
    return result
  end
  return "loss"
end

local function read_wallet(user_id)
  local objects = nk.storage_read({
    { collection = "wallet", key = "state", user_id = user_id }
  })
  if #objects == 0 then
    return { coins = 0, xp = 0 }
  end
  local value = objects[1].value
  return {
    coins = tonumber(value.coins) or 0,
    xp = tonumber(value.xp) or 0
  }
end

local function write_wallet(user_id, wallet)
  nk.storage_write({
    {
      collection = "wallet",
      key = "state",
      user_id = user_id,
      value = wallet,
      permission_read = 1,
      permission_write = 0
    }
  })
end

local function read_league_stats(user_id)
  local objects = nk.storage_read({
    { collection = "league", key = "player_stats", user_id = user_id }
  })
  if #objects == 0 then
    return { played = 0, wins = 0, draws = 0, losses = 0, points = 0 }
  end
  local value = objects[1].value
  return {
    played = tonumber(value.played) or 0,
    wins = tonumber(value.wins) or 0,
    draws = tonumber(value.draws) or 0,
    losses = tonumber(value.losses) or 0,
    points = tonumber(value.points) or 0
  }
end

local function write_league_stats(user_id, stats)
  nk.storage_write({
    {
      collection = "league",
      key = "player_stats",
      user_id = user_id,
      value = stats,
      permission_read = 1,
      permission_write = 0
    }
  })
end

local function award_player(user_id, result_key)
  local result = normalize_result(result_key)
  local reward = REWARDS[result] or REWARDS.loss
  local league_delta = LEAGUE_POINTS[result] or 0

  local wallet = read_wallet(user_id)
  wallet.coins = wallet.coins + (reward.coins or 0)
  wallet.xp = wallet.xp + (reward.xp or 0)
  write_wallet(user_id, wallet)

  local stats = read_league_stats(user_id)
  stats.played = stats.played + 1
  if result == "win" then
    stats.wins = stats.wins + 1
    stats.points = stats.points + league_delta
  elseif result == "draw" then
    stats.draws = stats.draws + 1
    stats.points = stats.points + league_delta
  else
    stats.losses = stats.losses + 1
  end
  write_league_stats(user_id, stats)

  return {
    coinsGranted = reward.coins or 0,
    xpGranted = reward.xp or 0,
    totalCoins = wallet.coins,
    totalXp = wallet.xp,
    leaguePlayed = stats.played,
    leagueWins = stats.wins,
    leagueDraws = stats.draws,
    leagueLosses = stats.losses,
    leaguePoints = stats.points,
    walletUpdated = true,
    leagueUpdated = true
  }
end

local function build_response(base, award)
  local out = {
    accepted = base.accepted == true,
    mismatch = base.mismatch == true,
    waiting = base.waiting == true,
    reason = base.reason or "",
    coinsGranted = 0,
    xpGranted = 0,
    totalCoins = 0,
    totalXp = 0,
    leaguePlayed = 0,
    leagueWins = 0,
    leagueDraws = 0,
    leagueLosses = 0,
    leaguePoints = 0,
    walletUpdated = false,
    leagueUpdated = false
  }
  if award ~= nil then
    out.coinsGranted = award.coinsGranted or 0
    out.xpGranted = award.xpGranted or 0
    out.totalCoins = award.totalCoins or 0
    out.totalXp = award.totalXp or 0
    out.leaguePlayed = award.leaguePlayed or 0
    out.leagueWins = award.leagueWins or 0
    out.leagueDraws = award.leagueDraws or 0
    out.leagueLosses = award.leagueLosses or 0
    out.leaguePoints = award.leaguePoints or 0
    out.walletUpdated = award.walletUpdated == true
    out.leagueUpdated = award.leagueUpdated == true
  end
  return nk.json_encode(out)
end

local function submit_match_result(context, payload)
  local data = parse_payload(context, payload)
  if data == nil or data.matchId == nil then
    return build_response({ accepted = false, reason = "invalid_payload" }, nil)
  end

  local match_id = data.matchId
  local user_id = context.user_id
  local result_key = normalize_result(data.result or "loss")
  local entry = {
    userId = user_id,
    localGoals = data.localGoals or 0,
    opponentGoals = data.opponentGoals or 0,
    result = result_key,
    durationSeconds = data.durationSeconds or 0,
    isHost = data.isHost == true
  }

  if pending[match_id] == nil then
    pending[match_id] = { submissions = {}, awarded = {} }
  end

  pending[match_id].submissions[user_id] = entry
  nk.logger_info(("submit_match_result match=%s user=%s score=%d-%d result=%s"):format(
    match_id, user_id, entry.localGoals, entry.opponentGoals, entry.result))

  local award = nil
  if pending[match_id].awarded[user_id] ~= true then
    award = award_player(user_id, result_key)
    pending[match_id].awarded[user_id] = true
    nk.logger_info(("award match=%s user=%s coins=%d xp=%d totals=%d/%d"):format(
      match_id, user_id, award.coinsGranted, award.xpGranted, award.totalCoins, award.totalXp))
  else
    local wallet = read_wallet(user_id)
    local stats = read_league_stats(user_id)
    award = {
      coinsGranted = 0,
      xpGranted = 0,
      totalCoins = wallet.coins,
      totalXp = wallet.xp,
      leaguePlayed = stats.played,
      leagueWins = stats.wins,
      leagueDraws = stats.draws,
      leagueLosses = stats.losses,
      leaguePoints = stats.points,
      walletUpdated = true,
      leagueUpdated = true
    }
  end

  local submissions = pending[match_id].submissions
  local count = 0
  local first = nil
  local second = nil
  for _, s in pairs(submissions) do
    count = count + 1
    if first == nil then
      first = s
    else
      second = s
    end
  end

  if count < 2 then
    return build_response({
      accepted = true,
      mismatch = false,
      waiting = true,
      reason = "waiting_opponent"
    }, award)
  end

  local mismatch = false
  if first ~= nil and second ~= nil then
    if first.localGoals ~= second.opponentGoals or first.opponentGoals ~= second.localGoals then
      mismatch = true
      nk.logger_warn(("score_mismatch match=%s a=%d-%d b=%d-%d"):format(
        match_id, first.localGoals, first.opponentGoals, second.localGoals, second.opponentGoals))
    end
  end

  pending[match_id] = nil

  return build_response({
    accepted = true,
    mismatch = mismatch,
    waiting = false,
    reason = mismatch and "score_mismatch" or "ok"
  }, award)
end
nk.register_rpc(submit_match_result, "submit_match_result")

local function get_wallet(context, payload)
  return nk.json_encode(read_wallet(context.user_id))
end
nk.register_rpc(get_wallet, "get_wallet")

-- Sadece boş wallet seed (client push authoritative overwrite yapmaz).
local function set_wallet(context, payload)
  local data = parse_payload(context, payload)
  if data == nil then
    return nk.json_encode({ ok = false, reason = "invalid_payload" })
  end

  local existing = nk.storage_read({
    { collection = "wallet", key = "state", user_id = context.user_id }
  })
  if #existing > 0 then
    return nk.json_encode({ ok = false, reason = "already_exists" })
  end

  write_wallet(context.user_id, {
    coins = math.max(0, tonumber(data.coins) or 0),
    xp = math.max(0, tonumber(data.xp) or 0)
  })
  return nk.json_encode({ ok = true, seeded = true })
end
nk.register_rpc(set_wallet, "set_wallet")

local function get_league_standings(context, payload)
  local stats = read_league_stats(context.user_id)
  return nk.json_encode({
    version = 1,
    player = stats,
    standings = {}
  })
end
nk.register_rpc(get_league_standings, "get_league_standings")
