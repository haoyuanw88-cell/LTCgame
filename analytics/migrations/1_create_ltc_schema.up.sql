CREATE TABLE player (
    p_id BIGSERIAL PRIMARY KEY,
    auth_uid CHAR(64) NOT NULL UNIQUE,
    p_name VARCHAR(40),
    birth_dt DATE,
    sex_cd VARCHAR(12),
    edu_yrs SMALLINT CHECK (edu_yrs BETWEEN 0 AND 30),
    create_ts TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen_ts TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX player_last_seen_idx ON player (last_seen_ts DESC);

CREATE TABLE game_session (
    s_id VARCHAR(40) PRIMARY KEY,
    p_id BIGINT NOT NULL REFERENCES player (p_id) ON DELETE CASCADE,
    game_name VARCHAR(64) NOT NULL,
    done_dt DATE NOT NULL,
    dur_ms INTEGER NOT NULL CHECK (dur_ms > 0)
);

CREATE INDEX game_session_player_done_idx ON game_session (p_id, done_dt DESC);

-- Pilot-only raw evidence. This table can be removed after the scoring
-- protocol is frozen and the team no longer needs trial-level recalculation.
CREATE TABLE trial_test (
    t_id BIGSERIAL PRIMARY KEY,
    s_id VARCHAR(40) NOT NULL REFERENCES game_session (s_id) ON DELETE CASCADE,
    q_no INTEGER NOT NULL CHECK (q_no > 0),
    cond_cd VARCHAR(64),
    target TEXT,
    answer TEXT,
    rt_ms INTEGER CHECK (rt_ms >= 0),
    UNIQUE (s_id, q_no)
);

CREATE TABLE metric (
    m_id BIGSERIAL PRIMARY KEY,
    s_id VARCHAR(40) NOT NULL REFERENCES game_session (s_id) ON DELETE CASCADE,
    dmn_name VARCHAR(64) NOT NULL,
    metric_name VARCHAR(64) NOT NULL,
    m_val DOUBLE PRECISION NOT NULL,
    valid_yn BOOLEAN NOT NULL DEFAULT FALSE,
    UNIQUE (s_id, dmn_name, metric_name)
);

CREATE INDEX metric_dashboard_idx
    ON metric (dmn_name, metric_name, valid_yn)
    WHERE valid_yn = TRUE;

CREATE TABLE wallet (
    p_id BIGINT PRIMARY KEY REFERENCES player (p_id) ON DELETE CASCADE,
    bal INTEGER NOT NULL DEFAULT 0 CHECK (bal >= 0)
);

CREATE TABLE coin_tx (
    tx_id VARCHAR(40) PRIMARY KEY,
    p_id BIGINT NOT NULL REFERENCES wallet (p_id) ON DELETE CASCADE,
    amt INTEGER NOT NULL CHECK (amt <> 0),
    create_ts TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX coin_tx_player_created_idx ON coin_tx (p_id, create_ts DESC);

CREATE TABLE item (
    i_id BIGSERIAL PRIMARY KEY,
    item_cd VARCHAR(40) NOT NULL UNIQUE,
    type_cd VARCHAR(32) NOT NULL,
    i_name VARCHAR(80) NOT NULL,
    price INTEGER NOT NULL CHECK (price >= 0)
);

CREATE TABLE inventory (
    inv_id BIGSERIAL PRIMARY KEY,
    p_id BIGINT NOT NULL REFERENCES player (p_id) ON DELETE CASCADE,
    i_id BIGINT NOT NULL REFERENCES item (i_id),
    qty INTEGER NOT NULL DEFAULT 0 CHECK (qty >= 0),
    UNIQUE (p_id, i_id)
);

CREATE TABLE coin_tx_item (
    txi_id BIGSERIAL PRIMARY KEY,
    tx_id VARCHAR(40) NOT NULL REFERENCES coin_tx (tx_id) ON DELETE CASCADE,
    i_id BIGINT NOT NULL REFERENCES item (i_id),
    qty INTEGER NOT NULL CHECK (qty > 0),
    unit_price INTEGER NOT NULL CHECK (unit_price >= 0),
    UNIQUE (tx_id, i_id)
);
