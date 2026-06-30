-- Create measurement_unit table
CREATE TABLE measurement_units (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    symbol VARCHAR(10)
);

CREATE INDEX idx_measurement_units_code ON measurement_units(code);

-- Seed initial units
INSERT INTO measurement_units (id, name, code, symbol) VALUES
    (gen_random_uuid(), 'Unidad', 'unit', 'ud'),
    (gen_random_uuid(), 'Kilogramo', 'kg', 'kg'),
    (gen_random_uuid(), 'Gramo', 'g', 'g'),
    (gen_random_uuid(), 'Litro', 'l', 'L'),
    (gen_random_uuid(), 'Mililitro', 'ml', 'mL'),
    (gen_random_uuid(), 'Metro', 'm', 'm'),
    (gen_random_uuid(), 'Centimetro', 'cm', 'cm'),
    (gen_random_uuid(), 'Caja', 'box', 'caja'),
    (gen_random_uuid(), 'Bulto', 'sack', 'bulto'),
    (gen_random_uuid(), 'Paquete', 'pack', 'paq');

-- Add measurement_unit_id to products and bundles
ALTER TABLE products ADD COLUMN measurement_unit_id UUID REFERENCES measurement_units(id) ON DELETE SET NULL;
CREATE INDEX idx_products_measurement_unit ON products(measurement_unit_id);

ALTER TABLE bundles ADD COLUMN measurement_unit_id UUID REFERENCES measurement_units(id) ON DELETE SET NULL;
CREATE INDEX idx_bundles_measurement_unit ON bundles(measurement_unit_id);
