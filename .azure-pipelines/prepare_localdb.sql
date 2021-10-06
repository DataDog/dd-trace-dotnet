sp_configure 'show advanced options', 1;
GO

RECONFIGURE;
GO

sp_configure 'user instance timeout', 30;
GO

RECONFIGURE;
GO