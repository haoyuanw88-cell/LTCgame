-- Make the wallet ledger explainable and retry-safe without adding another table.
-- src_cd: GAM=game reward, DAY=daily login, BUY=store purchase, ADJ=legacy adjustment.
-- op_id is the short idempotency key supplied by the business operation.

ALTER TABLE coin_tx ADD COLUMN src_cd CHAR(3) NOT NULL DEFAULT 'ADJ';
ALTER TABLE coin_tx ADD COLUMN op_id CHAR(9);
UPDATE coin_tx SET op_id = tx_id WHERE op_id IS NULL;
ALTER TABLE coin_tx ALTER COLUMN op_id SET NOT NULL;

ALTER TABLE coin_tx ADD CONSTRAINT coin_tx_src_cd_check
    CHECK (src_cd IN ('GAM', 'DAY', 'BUY', 'ADJ'));
ALTER TABLE coin_tx ADD CONSTRAINT coin_tx_player_op_key
    UNIQUE (p_id, op_id);

ALTER TABLE coin_tx ALTER COLUMN tx_id
    SET DEFAULT ('T' || LPAD(nextval('transaction_code_seq')::text, 8, '0'));

-- The current Unity shop contains these three food products. Prices remain
-- server-owned so a modified client cannot purchase at an invented price.
INSERT INTO item (item_cd, type_cd, i_name, price)
VALUES
    ('F_APPLE',  'FOOD', '蘋果', 1),
    ('F_BANANA', 'FOOD', '香蕉', 1),
    ('F_PINE',   'FOOD', '鳳梨', 1)
ON CONFLICT (item_cd) DO UPDATE SET
    type_cd = EXCLUDED.type_cd,
    i_name = EXCLUDED.i_name,
    price = EXCLUDED.price;
