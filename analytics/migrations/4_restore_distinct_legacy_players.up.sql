-- Undo migration 3: these records represent three distinct players, not one.
-- The original auth hashes for P000001 and P000003 were removed by that merge,
-- so restored rows receive unique recovery identities until each user is linked
-- again. Session ownership is restored from the pre-merge staging snapshot.

INSERT INTO player (p_id, auth_uid, p_name, create_dt, last_seen_ts)
VALUES
    ('P000001', '77bd33bf4d2469f156985e6fe51538085be3d53c65864043adb5e8e8ba44e2cd', NULL,
     DATE '2026-08-04', TIMESTAMPTZ '2026-08-04 00:00:00+00'),
    ('P000003', '1b024b3eeb331c062521dfd80062463fe97469fbc64f93bff36bcae5c50fca2d', NULL,
     DATE '2026-08-13', TIMESTAMPTZ '2026-08-17 00:00:00+00')
ON CONFLICT (p_id) DO NOTHING;

INSERT INTO wallet (p_id, bal)
VALUES ('P000001', 0), ('P000003', 0)
ON CONFLICT (p_id) DO NOTHING;

UPDATE game_session SET p_id = 'P000001' WHERE s_id = 'S00000001';
UPDATE game_session SET p_id = 'P000003' WHERE s_id IN ('S00000002', 'S00000013');
