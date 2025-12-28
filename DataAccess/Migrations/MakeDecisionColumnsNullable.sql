-- Make Decision columns nullable in Appointments table
-- This script preserves existing data

-- Check if columns exist and are NOT NULL
-- If they exist, alter them to allow NULL

BEGIN TRANSACTION;

-- Store Decision
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'StoreDecision' 
           AND IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE Appointments 
    ALTER COLUMN StoreDecision INT NULL;
    PRINT 'StoreDecision column altered to NULL';
END
ELSE
    PRINT 'StoreDecision is already nullable or does not exist';

-- FreeBarber Decision
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'FreeBarberDecision' 
           AND IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE Appointments 
    ALTER COLUMN FreeBarberDecision INT NULL;
    PRINT 'FreeBarberDecision column altered to NULL';
END
ELSE
    PRINT 'FreeBarberDecision is already nullable or does not exist';

-- Customer Decision
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'CustomerDecision' 
           AND IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE Appointments 
    ALTER COLUMN CustomerDecision INT NULL;
    PRINT 'CustomerDecision column altered to NULL';
END
ELSE
    PRINT 'CustomerDecision is already nullable or does not exist';

COMMIT TRANSACTION;

-- Verify the changes
SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Appointments' 
  AND COLUMN_NAME IN ('StoreDecision', 'FreeBarberDecision', 'CustomerDecision');

