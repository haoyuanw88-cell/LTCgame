-- The staging records created before Google sign-in all belong to the same
-- student test account. Merge them once into P000028. The exact-ID guard makes
-- this a no-op for a fresh or real multi-player database.
DO $$
DECLARE
    target_player CONSTANT CHAR(7) := 'P000028';
BEGIN
    IF EXISTS (SELECT 1 FROM player WHERE p_id = target_player)
       AND NOT EXISTS (
           SELECT 1 FROM player
           WHERE p_id NOT IN ('P000001', 'P000003', 'P000028')
       ) THEN
        UPDATE player target
        SET birth_dt = COALESCE(
                target.birth_dt,
                (SELECT source.birth_dt FROM player source
                 WHERE source.p_id IN ('P000003', 'P000001') AND source.birth_dt IS NOT NULL
                 ORDER BY source.p_id DESC LIMIT 1)
            ),
            sex_cd = COALESCE(
                target.sex_cd,
                (SELECT source.sex_cd FROM player source
                 WHERE source.p_id IN ('P000003', 'P000001') AND source.sex_cd IS NOT NULL
                 ORDER BY source.p_id DESC LIMIT 1)
            ),
            edu_yrs = COALESCE(
                target.edu_yrs,
                (SELECT source.edu_yrs FROM player source
                 WHERE source.p_id IN ('P000003', 'P000001') AND source.edu_yrs IS NOT NULL
                 ORDER BY source.p_id DESC LIMIT 1)
            )
        WHERE target.p_id = target_player;

        INSERT INTO inventory (p_id, i_id, qty)
        SELECT target_player, i_id, SUM(qty)::INTEGER
        FROM inventory
        WHERE p_id <> target_player
        GROUP BY i_id
        ON CONFLICT (p_id, i_id) DO UPDATE
        SET qty = inventory.qty + EXCLUDED.qty;

        UPDATE game_session SET p_id = target_player WHERE p_id <> target_player;
        UPDATE coin_tx SET p_id = target_player WHERE p_id <> target_player;

        UPDATE wallet target
        SET bal = (SELECT COALESCE(SUM(source.bal), 0)::INTEGER FROM wallet source)
        WHERE target.p_id = target_player;

        DELETE FROM inventory WHERE p_id <> target_player;
        DELETE FROM wallet WHERE p_id <> target_player;
        DELETE FROM player WHERE p_id <> target_player;
    END IF;
END $$;
