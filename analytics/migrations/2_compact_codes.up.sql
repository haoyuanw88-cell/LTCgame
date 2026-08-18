-- Compact identifiers and controlled codes used by the LTC graduation demo.
-- This migration preserves every existing row while changing all foreign keys
-- to the new human-readable identifiers.

CREATE SEQUENCE IF NOT EXISTS player_code_seq;
CREATE SEQUENCE IF NOT EXISTS session_code_seq;
CREATE SEQUENCE IF NOT EXISTS transaction_code_seq;

-- Reserve the next player number before converting the numeric key to P000000.
SELECT setval(
    'player_code_seq',
    COALESCE((SELECT MAX(p_id) FROM player), 0) + 1,
    FALSE
);

ALTER TABLE game_session DROP CONSTRAINT IF EXISTS game_session_p_id_fkey;
ALTER TABLE wallet DROP CONSTRAINT IF EXISTS wallet_p_id_fkey;
ALTER TABLE coin_tx DROP CONSTRAINT IF EXISTS coin_tx_p_id_fkey;
ALTER TABLE inventory DROP CONSTRAINT IF EXISTS inventory_p_id_fkey;

ALTER TABLE player ALTER COLUMN p_id DROP DEFAULT;
ALTER TABLE player
    ALTER COLUMN p_id TYPE CHAR(7)
    USING ('P' || LPAD(p_id::text, 6, '0'));
ALTER TABLE game_session
    ALTER COLUMN p_id TYPE CHAR(7)
    USING ('P' || LPAD(p_id::text, 6, '0'));
ALTER TABLE wallet
    ALTER COLUMN p_id TYPE CHAR(7)
    USING ('P' || LPAD(p_id::text, 6, '0'));
ALTER TABLE coin_tx
    ALTER COLUMN p_id TYPE CHAR(7)
    USING ('P' || LPAD(p_id::text, 6, '0'));
ALTER TABLE inventory
    ALTER COLUMN p_id TYPE CHAR(7)
    USING ('P' || LPAD(p_id::text, 6, '0'));

ALTER TABLE player ALTER COLUMN p_id
    SET DEFAULT ('P' || LPAD(nextval('player_code_seq')::text, 6, '0'));

ALTER TABLE game_session ADD CONSTRAINT game_session_p_id_fkey
    FOREIGN KEY (p_id) REFERENCES player (p_id) ON DELETE CASCADE;
ALTER TABLE wallet ADD CONSTRAINT wallet_p_id_fkey
    FOREIGN KEY (p_id) REFERENCES player (p_id) ON DELETE CASCADE;
ALTER TABLE coin_tx ADD CONSTRAINT coin_tx_p_id_fkey
    FOREIGN KEY (p_id) REFERENCES wallet (p_id) ON DELETE CASCADE;
ALTER TABLE inventory ADD CONSTRAINT inventory_p_id_fkey
    FOREIGN KEY (p_id) REFERENCES player (p_id) ON DELETE CASCADE;

UPDATE player
SET sex_cd = CASE LOWER(TRIM(sex_cd))
    WHEN 'male' THEN 'M'
    WHEN 'female' THEN 'F'
    WHEN 'other' THEN 'X'
    WHEN 'prefer_not_to_say' THEN 'N'
    WHEN 'm' THEN 'M'
    WHEN 'f' THEN 'F'
    WHEN 'x' THEN 'X'
    WHEN 'n' THEN 'N'
    ELSE NULL
END;
ALTER TABLE player ALTER COLUMN sex_cd TYPE CHAR(1) USING sex_cd::CHAR(1);
ALTER TABLE player RENAME COLUMN create_ts TO create_dt;
ALTER TABLE player ALTER COLUMN create_dt DROP DEFAULT;
ALTER TABLE player ALTER COLUMN create_dt TYPE DATE USING create_dt::DATE;
ALTER TABLE player ALTER COLUMN create_dt SET DEFAULT CURRENT_DATE;
UPDATE player SET last_seen_ts = date_trunc('minute', last_seen_ts);
ALTER TABLE player ALTER COLUMN last_seen_ts TYPE TIMESTAMPTZ(0);
ALTER TABLE player ALTER COLUMN last_seen_ts SET DEFAULT date_trunc('minute', NOW());

-- Replace every existing client GUID with a short server-issued session code.
ALTER TABLE game_session ADD COLUMN s_new CHAR(9);
WITH ranked AS (
    SELECT s_id, ROW_NUMBER() OVER (ORDER BY done_dt, s_id) AS seq_no
    FROM game_session
)
UPDATE game_session target
SET s_new = 'S' || LPAD(ranked.seq_no::text, 8, '0')
FROM ranked
WHERE target.s_id = ranked.s_id;
ALTER TABLE game_session ALTER COLUMN s_new SET NOT NULL;
SELECT setval(
    'session_code_seq',
    COALESCE((SELECT COUNT(*) FROM game_session), 0) + 1,
    FALSE
);

ALTER TABLE trial_test ADD COLUMN s_new CHAR(9);
UPDATE trial_test t
SET s_new = s.s_new
FROM game_session s
WHERE s.s_id = t.s_id;
ALTER TABLE trial_test ALTER COLUMN s_new SET NOT NULL;

ALTER TABLE metric ADD COLUMN s_new CHAR(9);
UPDATE metric m
SET s_new = s.s_new
FROM game_session s
WHERE s.s_id = m.s_id;
ALTER TABLE metric ALTER COLUMN s_new SET NOT NULL;

ALTER TABLE trial_test DROP CONSTRAINT IF EXISTS trial_test_s_id_fkey;
ALTER TABLE metric DROP CONSTRAINT IF EXISTS metric_s_id_fkey;
ALTER TABLE trial_test DROP CONSTRAINT IF EXISTS trial_test_s_id_q_no_key;
ALTER TABLE metric DROP CONSTRAINT IF EXISTS metric_s_id_dmn_name_metric_name_key;
ALTER TABLE game_session DROP CONSTRAINT IF EXISTS game_session_pkey;
DROP INDEX IF EXISTS metric_dashboard_idx;

ALTER TABLE trial_test DROP COLUMN s_id;
ALTER TABLE metric DROP COLUMN s_id;
ALTER TABLE game_session DROP COLUMN s_id;
ALTER TABLE trial_test RENAME COLUMN s_new TO s_id;
ALTER TABLE metric RENAME COLUMN s_new TO s_id;
ALTER TABLE game_session RENAME COLUMN s_new TO s_id;

ALTER TABLE game_session ALTER COLUMN s_id
    SET DEFAULT ('S' || LPAD(nextval('session_code_seq')::text, 8, '0'));
ALTER TABLE game_session ADD CONSTRAINT game_session_pkey PRIMARY KEY (s_id);
ALTER TABLE trial_test ADD CONSTRAINT trial_test_s_id_fkey
    FOREIGN KEY (s_id) REFERENCES game_session (s_id) ON DELETE CASCADE;
ALTER TABLE metric ADD CONSTRAINT metric_s_id_fkey
    FOREIGN KEY (s_id) REFERENCES game_session (s_id) ON DELETE CASCADE;

ALTER TABLE game_session RENAME COLUMN game_name TO game_cd;
ALTER TABLE game_session ALTER COLUMN game_cd TYPE CHAR(3) USING (
    CASE LOWER(TRIM(game_cd))
        WHEN 'stroop_color_match' THEN 'STP'
        WHEN 'stroop_color' THEN 'STP'
        WHEN 'number_order' THEN 'ORD'
        WHEN 'trail_making' THEN 'ORD'
        WHEN 'number_sum' THEN 'SUM'
        WHEN 'pipe_connection' THEN 'PIP'
        WHEN 'pipe_puzzle' THEN 'PIP'
        WHEN 'card_memory_battle' THEN 'CRD'
        WHEN 'memory_cards' THEN 'CRD'
        WHEN 'gopher_reaction' THEN 'GOP'
        WHEN 'body_whack_a_mole' THEN 'GOP'
        WHEN 'supermarket_shopping' THEN 'SUP'
        WHEN 'supermarket' THEN 'SUP'
        WHEN 'true_false_life_quiz' THEN 'QIZ'
        WHEN 'life_quiz' THEN 'QIZ'
        ELSE 'UNK'
    END
)::CHAR(3);

UPDATE trial_test SET cond_cd = CASE LOWER(TRIM(cond_cd))
    WHEN 'match_low_conflict' THEN 'MLC'
    WHEN 'mismatch_low_conflict' THEN 'XLC'
    WHEN 'match_high_conflict' THEN 'MHC'
    WHEN 'mismatch_high_conflict' THEN 'XHC'
    WHEN 'positive_only' THEN 'POS'
    WHEN 'positive_and_negative' THEN 'PAN'
    WHEN 'target_sum' THEN 'TSM'
    WHEN 'response' THEN 'RSP'
    WHEN 'round_summary' THEN 'RND'
    WHEN 'selection' THEN 'SEL'
    ELSE NULLIF(UPPER(SUBSTRING(REGEXP_REPLACE(cond_cd, '[^A-Za-z0-9]', '', 'g') FROM 1 FOR 4)), '')
END;
ALTER TABLE trial_test ALTER COLUMN cond_cd TYPE VARCHAR(4);

ALTER TABLE metric RENAME COLUMN dmn_name TO dmn_cd;
ALTER TABLE metric RENAME COLUMN metric_name TO metric_cd;

ALTER TABLE metric ALTER COLUMN dmn_cd TYPE VARCHAR(4) USING (
    CASE LOWER(TRIM(dmn_cd))
        WHEN 'attention_inhibition' THEN 'ATT'
        WHEN 'processing_speed' THEN 'SPD'
        WHEN 'executive_reasoning' THEN 'EXE'
        WHEN 'executive_function' THEN 'EXE'
        WHEN 'visual_working_memory' THEN 'VWM'
        WHEN 'visuospatial_planning' THEN 'VSP'
        WHEN 'language' THEN 'LNG'
        WHEN 'episodic_memory' THEN 'EPM'
        WHEN 'orientation' THEN 'ORI'
        WHEN 'memory' THEN 'MEM'
        WHEN 'spatial_reasoning' THEN 'SPR'
        WHEN 'motor_coordination' THEN 'MOT'
        WHEN 'wellbeing' THEN 'WEL'
        ELSE 'UNK'
    END
)::VARCHAR(4);

ALTER TABLE metric ALTER COLUMN metric_cd TYPE VARCHAR(5) USING (
    CASE LOWER(TRIM(metric_cd))
        WHEN 'valid_trial_count' THEN 'VTC'
        WHEN 'excluded_trial_rate' THEN 'EXR'
        WHEN 'completion_rate' THEN 'CPR'
        WHEN 'accuracy' THEN 'ACC'
        WHEN 'omission_rate' THEN 'OMR'
        WHEN 'median_correct_rt' THEN 'MRT'
        WHEN 'rt_mad' THEN 'MAD'
        WHEN 'robust_rt_variability' THEN 'RTV'
        WHEN 'inverse_efficiency' THEN 'IES'
        WHEN 'task_performance_index' THEN 'TPI'
        WHEN 'low_interference_median_rt' THEN 'LRT'
        WHEN 'high_interference_median_rt' THEN 'HRT'
        WHEN 'stroop_rt_interference' THEN 'SRI'
        WHEN 'interference_ratio' THEN 'IR'
        WHEN 'stroop_error_interference' THEN 'SEI'
        WHEN 'trail_total_completion_time' THEN 'TCT'
        WHEN 'trail_sequence_error_count' THEN 'SEC'
        WHEN 'trail_completed_round_count' THEN 'CRC'
        WHEN 'trail_round_completion_rate' THEN 'RCR'
        WHEN 'planning_optimal_solution_rate' THEN 'OSR'
        WHEN 'planning_median_excess_moves' THEN 'EXM'
        WHEN 'planning_median_initial_thinking_time' THEN 'PIT'
        WHEN 'planning_median_execution_time' THEN 'EXT'
        WHEN 'planning_rule_violation_count' THEN 'RVC'
        WHEN 'planning_round_completion_rate' THEN 'PCR'
        ELSE NULLIF(UPPER(SUBSTRING(REGEXP_REPLACE(metric_cd, '[^A-Za-z0-9]', '', 'g') FROM 1 FOR 5)), '')
    END
)::VARCHAR(5);

ALTER TABLE metric ALTER COLUMN m_val TYPE NUMERIC(12,3)
    USING ROUND(m_val::NUMERIC, 3);
ALTER TABLE trial_test ADD CONSTRAINT trial_test_s_id_q_no_key UNIQUE (s_id, q_no);
ALTER TABLE metric ADD CONSTRAINT metric_s_id_dmn_cd_metric_cd_key UNIQUE (s_id, dmn_cd, metric_cd);
CREATE INDEX metric_dashboard_idx
    ON metric (dmn_cd, metric_cd, valid_yn)
    WHERE valid_yn = TRUE;

-- Compact transaction IDs. Existing rows, if any, are treated as adjustments.
ALTER TABLE coin_tx ADD COLUMN tx_new CHAR(9);
WITH ranked AS (
    SELECT tx_id, ROW_NUMBER() OVER (ORDER BY create_ts, tx_id) AS seq_no
    FROM coin_tx
)
UPDATE coin_tx target
SET tx_new = 'A' || LPAD(ranked.seq_no::text, 8, '0')
FROM ranked
WHERE target.tx_id = ranked.tx_id;
ALTER TABLE coin_tx ALTER COLUMN tx_new SET NOT NULL;
SELECT setval(
    'transaction_code_seq',
    COALESCE((SELECT COUNT(*) FROM coin_tx), 0) + 1,
    FALSE
);

ALTER TABLE coin_tx_item ADD COLUMN tx_new CHAR(9);
UPDATE coin_tx_item d
SET tx_new = t.tx_new
FROM coin_tx t
WHERE t.tx_id = d.tx_id;
ALTER TABLE coin_tx_item ALTER COLUMN tx_new SET NOT NULL;

ALTER TABLE coin_tx_item DROP CONSTRAINT IF EXISTS coin_tx_item_tx_id_fkey;
ALTER TABLE coin_tx_item DROP CONSTRAINT IF EXISTS coin_tx_item_tx_id_i_id_key;
ALTER TABLE coin_tx_item DROP CONSTRAINT IF EXISTS coin_tx_item_pkey;
ALTER TABLE coin_tx DROP CONSTRAINT IF EXISTS coin_tx_pkey;
ALTER TABLE coin_tx_item DROP COLUMN tx_id;
ALTER TABLE coin_tx DROP COLUMN tx_id;
ALTER TABLE coin_tx_item RENAME COLUMN tx_new TO tx_id;
ALTER TABLE coin_tx RENAME COLUMN tx_new TO tx_id;
ALTER TABLE coin_tx ALTER COLUMN tx_id
    SET DEFAULT ('A' || LPAD(nextval('transaction_code_seq')::text, 8, '0'));
ALTER TABLE coin_tx ADD CONSTRAINT coin_tx_pkey PRIMARY KEY (tx_id);
ALTER TABLE coin_tx_item ADD CONSTRAINT coin_tx_item_tx_id_fkey
    FOREIGN KEY (tx_id) REFERENCES coin_tx (tx_id) ON DELETE CASCADE;

ALTER TABLE coin_tx ADD COLUMN s_id CHAR(9);
ALTER TABLE coin_tx ADD CONSTRAINT coin_tx_s_id_fkey
    FOREIGN KEY (s_id) REFERENCES game_session (s_id);
CREATE UNIQUE INDEX coin_tx_reward_session_idx ON coin_tx (s_id) WHERE s_id IS NOT NULL;
UPDATE coin_tx SET create_ts = date_trunc('second', create_ts);
ALTER TABLE coin_tx ALTER COLUMN create_ts TYPE TIMESTAMPTZ(0);

-- Apply the approved composite keys even when migration 1 originally created
-- temporary surrogate keys in an older staging database.
ALTER TABLE inventory DROP CONSTRAINT IF EXISTS inventory_pkey;
ALTER TABLE inventory DROP CONSTRAINT IF EXISTS inventory_p_id_i_id_key;
ALTER TABLE inventory DROP COLUMN IF EXISTS inv_id;
ALTER TABLE inventory ADD CONSTRAINT inventory_pkey PRIMARY KEY (p_id, i_id);

ALTER TABLE coin_tx_item DROP COLUMN IF EXISTS txi_id;
ALTER TABLE coin_tx_item ADD CONSTRAINT coin_tx_item_pkey PRIMARY KEY (tx_id, i_id);

ALTER TABLE item ALTER COLUMN item_cd TYPE VARCHAR(12);
ALTER TABLE item ALTER COLUMN type_cd TYPE VARCHAR(4);
