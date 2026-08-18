-- Migration 4 is a one-time staging repair. On a brand-new environment there
-- are no matching historical sessions, so remove its recovery placeholders.
DELETE FROM wallet
WHERE p_id IN ('P000001', 'P000003')
  AND NOT EXISTS (SELECT 1 FROM game_session WHERE game_session.p_id = wallet.p_id);

DELETE FROM player
WHERE auth_uid IN (
    '77bd33bf4d2469f156985e6fe51538085be3d53c65864043adb5e8e8ba44e2cd',
    '1b024b3eeb331c062521dfd80062463fe97469fbc64f93bff36bcae5c50fca2d'
)
  AND NOT EXISTS (SELECT 1 FROM game_session WHERE game_session.p_id = player.p_id);
