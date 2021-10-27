sp_configure 'show advanced options', 1;
GO

RECONFIGURE;
GO

sp_configure 'user instance timeout', 60;
GO

RECONFIGURE;
GO