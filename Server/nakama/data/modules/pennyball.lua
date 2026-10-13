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

local function read_profile(user_id)
  local objects = nk.storage_read({
    { collection = "profile", key = "state", user_id = user_id }
  })
  if #objects == 0 then
    return { avatar_index = 0, display_name = "" }
  end
  local value = objects[1].value
  return {
    avatar_index = tonumber(value.avatar_index) or 0,
    display_name = value.display_name or ""
  }
end

-- Avatar ve display_name ayrı ayrı güncellenebilir — var olanı okuyup sadece
-- verilen alanı değiştirip geri yazıyoruz (diğerini sıfırlamamak için).
local function write_profile(user_id, updates)
  local current = read_profile(user_id)
  if updates.avatar_index ~= nil then
    current.avatar_index = updates.avatar_index
  end
  if updates.display_name ~= nil then
    current.display_name = updates.display_name
  end

  nk.storage_write({
    {
      collection = "profile",
      key = "state",
      user_id = user_id,
      value = current,
      permission_read = 1,
      permission_write = 0
    }
  })

  return current
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

-- Profil (avatar + display_name): wallet'tan farklı olarak her değişiklikte
-- serbestçe üzerine yazılır (seed değil).
local function get_avatar(context, payload)
  return nk.json_encode(read_profile(context.user_id))
end
nk.register_rpc(get_avatar, "get_avatar")

local function set_avatar(context, payload)
  local data = parse_payload(context, payload)
  if data == nil or data.avatar_index == nil then
    return nk.json_encode({ ok = false, reason = "invalid_payload" })
  end

  local avatar_index = math.max(0, math.floor(tonumber(data.avatar_index) or 0))
  write_profile(context.user_id, { avatar_index = avatar_index })
  return nk.json_encode({ ok = true, avatar_index = avatar_index })
end
nk.register_rpc(set_avatar, "set_avatar")

local function set_display_name(context, payload)
  local data = parse_payload(context, payload)
  if data == nil or data.display_name == nil or data.display_name == "" then
    return nk.json_encode({ ok = false, reason = "invalid_payload" })
  end

  local display_name = tostring(data.display_name)
  write_profile(context.user_id, { display_name = display_name })
  return nk.json_encode({ ok = true, display_name = display_name })
end
nk.register_rpc(set_display_name, "set_display_name")

local function get_league_standings(context, payload)
  local stats = read_league_stats(context.user_id)
  return nk.json_encode({
    version = 1,
    player = stats,
    standings = {}
  })
end
nk.register_rpc(get_league_standings, "get_league_standings")

-- Arkadaşla oynama: 6 haneli oda kodu ile Create/Join.
local ROOM_CODE_COLLECTION = "room_codes"
local ROOM_CODE_TTL_SECONDS = 120
local ROOM_CODE_CREATE_ATTEMPTS = 5
-- room_codes kayıtları herhangi bir oyuncuya değil "sisteme" ait — kodu bilen
-- herkes (host/guest) nk.storage_read ile okuyabilsin diye sabit nil-UUID kullanılıyor.
local ROOM_CODE_SYSTEM_USER_ID = "00000000-0000-0000-0000-000000000000"

local function read_room(code)
  local objects = nk.storage_read({
    { collection = ROOM_CODE_COLLECTION, key = code, user_id = ROOM_CODE_SYSTEM_USER_ID }
  })
  if #objects == 0 then
    return nil, nil
  end
  return objects[1].value, objects[1].version
end

local function generate_room_code()
  return tostring(math.random(100000, 999999))
end

local function create_room_code(context, payload)
  local profile = read_profile(context.user_id)
  local host_name = (profile.display_name ~= "" and profile.display_name) or context.username or ""
  local host_avatar_index = profile.avatar_index
  local now = os.time()

  for _ = 1, ROOM_CODE_CREATE_ATTEMPTS do
    local code = generate_room_code()
    local value = {
      code = code,
      host_user_id = context.user_id,
      host_name = host_name,
      host_avatar_index = host_avatar_index,
      guest_user_id = "",
      guest_name = "",
      guest_avatar_index = 0,
      photon_room_name = "pb_rc_" .. code,
      status = "waiting",
      created_at = now,
      expires_at = now + ROOM_CODE_TTL_SECONDS
    }

    local ok, err = pcall(nk.storage_write, {
      {
        collection = ROOM_CODE_COLLECTION,
        key = code,
        user_id = ROOM_CODE_SYSTEM_USER_ID,
        value = value,
        version = "*",
        permission_read = 2,
        permission_write = 0
      }
    })

    if not ok then
      nk.logger_warn(("create_room_code storage_write failed: %s"):format(tostring(err)))
    end

    if ok then
      return nk.json_encode({
        ok = true,
        code = code,
        photon_room_name = value.photon_room_name,
        expires_at = value.expires_at
      })
    end
  end

  return nk.json_encode({ ok = false, reason = "generation_failed" })
end
nk.register_rpc(create_room_code, "create_room_code")

local function join_room_code(context, payload)
  local data = parse_payload(context, payload)
  if data == nil or data.code == nil or data.code == "" then
    return nk.json_encode({ ok = false, reason = "invalid_payload" })
  end

  local code = tostring(data.code)
  local value, version = read_room(code)
  if value == nil then
    return nk.json_encode({ ok = false, reason = "not_found" })
  end

  local now = os.time()
  if now > (tonumber(value.expires_at) or 0) then
    value.status = "expired"
    pcall(nk.storage_write, {
      { collection = ROOM_CODE_COLLECTION, key = code, user_id = ROOM_CODE_SYSTEM_USER_ID, value = value, permission_read = 2, permission_write = 0 }
    })
    return nk.json_encode({ ok = false, reason = "expired" })
  end

  if value.status ~= "waiting" then
    return nk.json_encode({ ok = false, reason = "full_or_cancelled" })
  end

  local guest_profile = read_profile(context.user_id)
  value.guest_user_id = context.user_id
  value.guest_name = (guest_profile.display_name ~= "" and guest_profile.display_name) or context.username or ""
  value.guest_avatar_index = guest_profile.avatar_index
  value.status = "full"

  local ok = pcall(nk.storage_write, {
    {
      collection = ROOM_CODE_COLLECTION,
      key = code,
      user_id = ROOM_CODE_SYSTEM_USER_ID,
      value = value,
      version = version,
      permission_read = 2,
      permission_write = 0
    }
  })

  if not ok then
    return nk.json_encode({ ok = false, reason = "race_lost" })
  end

  return nk.json_encode({
    ok = true,
    photon_room_name = value.photon_room_name,
    host_user_id = value.host_user_id,
    host_name = value.host_name,
    host_avatar_index = value.host_avatar_index or 0
  })
end
nk.register_rpc(join_room_code, "join_room_code")

-- Host, Photon üzerinden rakibin odaya katıldığını tespit ettikten sonra bu RPC ile
-- misafirin kimliğini/avatarını öğrenir (join_room_code'un host'a dönen bir karşılığı yok).
local function get_room_code_guest(context, payload)
  local data = parse_payload(context, payload)
  if data == nil or data.code == nil or data.code == "" then
    return nk.json_encode({ ok = false, reason = "invalid_payload" })
  end

  local code = tostring(data.code)
  local value = read_room(code)
  if value == nil then
    return nk.json_encode({ ok = false, reason = "not_found" })
  end

  if value.host_user_id ~= context.user_id then
    return nk.json_encode({ ok = false, reason = "not_owner" })
  end

  if value.status ~= "full" or value.guest_user_id == nil or value.guest_user_id == "" then
    return nk.json_encode({ ok = false, reason = "no_guest_yet" })
  end

  return nk.json_encode({
    ok = true,
    guest_user_id = value.guest_user_id,
    guest_name = value.guest_name,
    guest_avatar_index = value.guest_avatar_index or 0
  })
end
nk.register_rpc(get_room_code_guest, "get_room_code_guest")

local function cancel_room_code(context, payload)
  local data = parse_payload(context, payload)
  if data == nil or data.code == nil or data.code == "" then
    return nk.json_encode({ ok = false, reason = "invalid_payload" })
  end

  local code = tostring(data.code)
  local value, version = read_room(code)
  if value == nil then
    return nk.json_encode({ ok = false, reason = "not_found" })
  end

  if value.host_user_id ~= context.user_id then
    return nk.json_encode({ ok = false, reason = "not_owner" })
  end

  value.status = "cancelled"
  local ok = pcall(nk.storage_write, {
    {
      collection = ROOM_CODE_COLLECTION,
      key = code,
      user_id = ROOM_CODE_SYSTEM_USER_ID,
      value = value,
      version = version,
      permission_read = 2,
      permission_write = 0
    }
  })

  if not ok then
    return nk.json_encode({ ok = false, reason = "race_lost" })
  end

  return nk.json_encode({ ok = true })
end
nk.register_rpc(cancel_room_code, "cancel_room_code")
