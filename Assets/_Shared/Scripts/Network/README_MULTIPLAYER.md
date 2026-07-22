# PennyBall Multiplayer Setup

## Stack

| Layer | Tech | Role |
|-------|------|------|
| Auth / matchmaking / result | Nakama | Device auth, matchmaker, `submit_match_result` → wallet/lig |
| Quick match fallback | Photon session adı | Nakama yokken Play ile 1v1 |
| **In-match sync** | **Photon Fusion Shared Mode** | Shot / gol / süre / MatchStart |

## Play ile eşleşme (önerilen test)

Editor'da matchmaking **varsayılan açık**.

1. ParrelSync: **main + clone** Play Mode
2. İkisinde de Main Menu → **Play** (~2 dk içinde, aynı lig)
3. Matching panel'de **Photon odasında 2. oyuncu beklenir** (Create/Join ile aynı)
4. Eşleşince VS → Game → **hemen 3-2-1** (rakip zaten odada)

Nakama matchmaker Play path'te **atlanır** (45sn beklemeyi öldürmek için). Auth/wallet ayrı.

Menü: **PennyBall → Online → Enable Matchmaking (Play test)**

## Photon-only proto (eski)

1. **Create Photon Test Room And Load Game**
2. Clone: **Join … From Clipboard** / Fixed Debug Room

## Sync

- MatchStart: 2. oyuncu gelince countdown
- Shot / Goal / Time: `MatchShotRelay` RPC

## Feature flags

- `OnlineMatchmakingEnabled` — Play online path (Editor default ON)
- `OnlineOnlyMatches` — bot fallback yok
- `UseOnlineWallet` — online maç ödülü Nakama `submit_match_result` (Editor default ON)
- `UseOnlineLeague` — sunucu player_stats log/sync (tam lig tablosu sonra; Editor default ON)
- Maç içi kanal: Photon (AppId zorunlu)

## Maç sonucu → wallet / lig (Nakama)

Docker Nakama ayakta olmalı: `cd docker/nakama && docker compose up -d`

1. Online maç biter → `LeagueService.RegisterMatchResult`
2. `MatchResultSubmitter` → RPC `submit_match_result`
3. Sunucu: skor tutarlılığı + coin/XP (40/20/10 + 10 XP) + `league/player_stats`
4. Client (`UseOnlineWallet`): `WalletService.SetTotals` sunucu yanıtından; Nakama yoksa local fallback
5. Local bot ligi sezonu korunur; sunucu stats ayrı birikir (`OnlineOnlyMatches` iken absolute sync)

Menü: **PennyBall → Online → Toggle Online Wallet / League**
