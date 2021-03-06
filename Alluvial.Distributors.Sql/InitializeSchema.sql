USE [AlluvialSqlDistributor]
GO

IF (NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Alluvial')) 
BEGIN
    EXEC ('CREATE SCHEMA [Alluvial]')
END
GO

IF object_id('[Alluvial].[Tokens]') IS NULL
BEGIN
	CREATE SEQUENCE [Alluvial].[Tokens] 
	 AS [int]
	 START WITH 1
	 INCREMENT BY 1
	 MINVALUE 1
	 MAXVALUE 2147483647
	 CYCLE 
	 CACHE 
END
GO

IF object_id('[Alluvial].[AcquireLease]') IS NULL
	exec('CREATE PROCEDURE [Alluvial].[AcquireLease] AS SELECT 1')
GO

ALTER PROCEDURE [Alluvial].[AcquireLease]
	@pool nvarchar(50),
	@waitIntervalMilliseconds int = 5000, 
	@leaseDurationMilliseconds int = 60000
	AS
	BEGIN

	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL SERIALIZABLE

	DECLARE @resourceName nvarchar(50)
	DECLARE @now datetimeoffset
	DECLARE @token int

	SELECT @token = NEXT VALUE FOR Tokens
	SELECT @now = SYSDATETIMEOFFSET()
	
	BEGIN TRAN

	SELECT @resourceName = (SELECT TOP 1 ResourceName FROM Leases WITH (XLOCK,ROWLOCK)
		WHERE 
			Pool = @pool
				AND 
			(Expires IS NULL OR Expires < @now) 
				AND 
			DATEADD(MILLISECOND, @waitIntervalMilliseconds, LastReleased) < @now 
			ORDER BY LastReleased)

	UPDATE Leases
		SET LastGranted = @now,
			Expires = DATEADD(MILLISECOND, @leaseDurationMilliseconds, @now),
			Token = @token
		WHERE 
			ResourceName = @resourceName
				AND 
			Pool = @pool

	COMMIT TRAN

	SELECT * FROM Leases 
	WHERE ResourceName = @resourceName 
	AND Token = @token

	END


GO

IF object_id('[Alluvial].[ExtendLease]') IS NULL
	exec('CREATE PROCEDURE [Alluvial].[ExtendLease] AS SELECT 1')
GO

ALTER PROCEDURE [Alluvial].[ExtendLease]
	@resourceName nvarchar(50),
	@byMilliseconds int, 
	@token int  
	AS
	BEGIN

	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL SERIALIZABLE

	BEGIN TRAN

	DECLARE @expires datetimeoffset(7)

	SELECT @expires = 
	(SELECT Expires FROM Leases WITH (XLOCK,ROWLOCK)
		WHERE 
			ResourceName = @resourceName 
				AND 
			Token = @token)

	UPDATE Leases
		SET Expires = DATEADD(MILLISECOND, @byMilliseconds, @expires)
		WHERE 
			ResourceName = @resourceName 
				AND 
			Token = @token
				AND 
			Expires >= SYSDATETIMEOFFSET()

	IF @@ROWCOUNT = 0
		BEGIN
			ROLLBACK TRAN;
			THROW 50000, 'Lease could not be extended', 1;
		END
	ELSE
		COMMIT TRAN;
	END

GO

IF object_id('[Alluvial].[ReleaseLease]') IS NULL
	exec('CREATE PROCEDURE [Alluvial].[ReleaseLease] AS SELECT 1')
GO

ALTER PROCEDURE [Alluvial].[ReleaseLease]
	@resourceName nvarchar(50)  , 
	@token int  
	AS
	BEGIN
	SET NOCOUNT ON;

	DECLARE @now DATETIMEOFFSET(7)
	SELECT @now = SYSDATETIMEOFFSET()

	UPDATE Leases
	SET LastReleased = @now,
	    Expires = null
	WHERE ResourceName = @resourceName 
	AND Token = @token

	SELECT LastReased = @now

	END

GO

IF object_id('[Alluvial].[Leases]') IS NULL
BEGIN
	CREATE TABLE [Alluvial].[Leases](
		[ResourceName] [nvarchar](50) NOT NULL,
		[Pool] [nvarchar](50) NOT NULL,
		[LastGranted] [datetimeoffset](7) NULL,
		[LastReleased] [datetimeoffset](7) NULL,
		[Expires] [datetimeoffset](7) NULL,
		[Token] [int] NULL,
	 CONSTRAINT [PK_Leases_1] PRIMARY KEY CLUSTERED 
	(
		[ResourceName] ASC,
		[Pool] ASC
	)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
	) ON [PRIMARY]

	CREATE NONCLUSTERED INDEX [IX_Leases.Token] ON [Alluvial].[Leases]
	(
		[Token] ASC
	)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
END
GO
