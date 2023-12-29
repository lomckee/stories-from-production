---
layout: post
title:  "Microsoft SQL Server Implicit Conversions"
date:   2023-12-27 18:51:40 +0000
categories: sql server
---

## Implicit Conversions in Microsoft SQL Server

A very common issue is an implicit conversion. They occur when SQL Server is told to compare two values of differing datatypes.
They often cause SQL Server to perform an index scan instead of an index seek. This can cause unintentional performance impact.

This post will review several implicit conversion scenarios and how to fix them.
We will start by looking at an implicit conversion introduced in a where clause; followed by an implicit conversion caused by a comparison between two columns; finally we will look at one caused by a misconfiguration in Entity Framework. They're all the same problem with very similar solutions.

All demos will be done using code that will generate all the tables and data sufficient for a demo. All code used will be provided as part of the blog post.

## Generate Some Data

{% highlight sql %}
CREATE TABLE Lefty
(
	Id INT IDENTITY(1,1),
	ExternalId VARCHAR(100) NOT NULL,
	CONSTRAINT PK_Lefty PRIMARY KEY (Id)
)

CREATE TABLE Righty
(
	Id INT IDENTITY(1,1),
	ExternalId VARCHAR(100) NOT NULL,
	ExternalIdNumber INT NOT NULL,
	CONSTRAINT PK_Righty PRIMARY KEY (Id)
)

/* Add data - about 6.5million rows */
INSERT INTO Lefty (ExternalId)
SELECT CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS VARCHAR(10))
FROM master.dbo.spt_values spt1
CROSS APPLY master.dbo.spt_values spt2;

INSERT INTO Righty (ExternalId, ExternalIdNumber)
SELECT ExternalId, ROW_NUMBER() OVER (ORDER BY Id) AS ExternalIdNumber
FROM Lefty;

/* Add indexes */
CREATE NONCLUSTERED INDEX IX_Righty_ExternalId
ON dbo.Righty ( ExternalId );

CREATE NONCLUSTERED INDEX IX_Righty_ExternalIdNumber
ON dbo.Righty ( ExternalIdNumber );

CREATE NONCLUSTERED INDEX IX_Lefty_ExternalId
ON dbo.Lefty ( ExternalId );

{% endhighlight %}

## Implicit Conversion Introduced in Where Clause

### Problem

In this situation the value that is being filtered by is a type mismatch to the datatype in the table.
In the code snippet below, a lookup is performed for the ExternalId value `156` as an integer. Examining the plan generated, a warning is produced about an implicit conversion and an index scan is performed on IX_Righty_ExternalId.

If `SET STATISTICS IO ON;` is also set, it's observable that a large number of logical reads is performed, in this case just over 18,000.

{% highlight sql %}
SET STATISTICS IO ON;

SELECT Id, ExternalId, ExternalIdNumber
FROM Righty r
WHERE r.ExternalId = 156
{% endhighlight %}

`Table 'Righty'. Scan count 11, logical reads 18002`

![Where Clause implicit conversion](/assets/2023-12-27-implicit-conversions/01-where-clause.png)

### Solution

Simply replace the integer in the where clause with a datatype that matches the value in the table, in this case a varchar(100).

{% highlight sql %}
SELECT Id, ExternalId, ExternalIdNumber
FROM Righty r
WHERE r.ExternalId = '156'
{% endhighlight %}

`Table 'Righty'. Scan count 1, logical reads 6`

![Where Clause implicit conversion](/assets/2023-12-27-implicit-conversions/02-where-clause-solution.png)

## Implicit Conversion Caused by Joining on Two Different Datatypes

### Problem

Table A has a field that is a foreign key of Table B; however, the datatypes of these fields differ. A join is performed on them as part of a query. Note that having an actual foreign key in place would prevent this scenario, but as is often the case in the real world, explicit foreign key constraints are often neglected.

For this problem we will change the table structure a little bit to better represent what I've seen in the real world.

You may note that the following tables are missing explicit foreign key constraints. This is intentional for this demo as in the real world I have encountered numerous times foreign keys without the constraint explicitly declared. We will see how this also impacts performance. Additionally, you can't have a foreign key between two fields with mismatched data types.

{% highlight sql %}
CREATE TABLE ExternalSourceData
(
	Id VARCHAR(100) NOT NULL,
	CONSTRAINT PK_ExternalSourceData PRIMARY KEY (Id)
)

CREATE TABLE InternalData
(
	Id INT IDENTITY(1,1),
	ExternalId VARCHAR(100) NOT NULL,
	ExternalIdNumber INT NOT NULL,
	CONSTRAINT PK_InternalData PRIMARY KEY (Id)
)

/* Add data */
INSERT INTO ExternalSourceData (Id)
SELECT CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS VARCHAR(10))
FROM master.dbo.spt_values spt1
CROSS APPLY master.dbo.spt_values spt2;

INSERT INTO InternalData (ExternalId, ExternalIdNumber)
SELECT Id, CAST(Id AS VARCHAR(10)) AS ExternalIdNumber
FROM dbo.ExternalSourceData;

/* Add indexes */
CREATE NONCLUSTERED INDEX IX_InternalData_ExternalId
ON dbo.InternalData ( ExternalId );

CREATE NONCLUSTERED INDEX IX_InternalData_ExternalIdNumber
ON dbo.InternalData ( ExternalIdNumber );

/* Test queries */
SELECT TOP 1000 id.Id, id.ExternalId, id.ExternalIdNumber
FROM dbo.ExternalSourceData esd
JOIN dbo.InternalData id
ON esd.Id = id.ExternalId

SELECT TOP 1000 id.Id, id.ExternalId, id.ExternalIdNumber
FROM dbo.ExternalSourceData esd
JOIN dbo.InternalData id
ON esd.Id = id.ExternalIdNumber

{% endhighlight %}

With an index on `ExternalId` and `ExternalIdNumber` typically we would expect an index seek on `IX_InternalData_ExternalId` and `IX_InternalData_ExternalIdNumber` when joining onto those columns; due to the datatype mismatch between `ExternalSourceData.Id` and `InternalData.ExternalIdNumber` an implicit conversion occurs and a scan is performed instead of a seek.

![Join Implicit Conversion](/assets/2023-12-27-implicit-conversions/03-join-mismatch.png)

With `SET STATISTICS IO ON;` set we can see a large number of scans are performed and over 6000 logical reads occur with this data set.

`Table 'InternalData'. Scan count 1000, logical reads 6288,`

`Table 'ExternalSourceData'. Scan count 1, logical reads 33`

Joining onto `InternalData.ExternalId`, which has the same datatype as `ExternalSourceData.Id`, there is a difference in the amount of work done

`Table 'InternalData'. Scan count 1, logical reads 3096`

`Table 'ExternalSourceData'. Scan count 1, logical reads 33`

![Join Same Type](/assets/2023-12-27-implicit-conversions/04-join-same-type.png)

### Solution

In the real world, you wouldn't have two columns of differing data types representing the same value, that's just silly.
The solution would generally be to update the datatype to the most appropriate one for the situation. If all the values are integers for example, both columns should be integers; however, if it's variable data then varchar would be more appropriate.

{% highlight sql %}
ALTER TABLE dbo.InternalData
ALTER COLUMN ExternalIdNumber VARCHAR(100) NOT NULL
{% endhighlight %}

You may need to drop and recreate dependent objects.

{% highlight sql %}
DROP INDEX IF EXISTS IX_InternalData_ExternalIdNumber ON dbo.InternalData

CREATE NONCLUSTERED INDEX IX_InternalData_ExternalIdNumber
ON dbo.InternalData ( ExternalIdNumber );
{% endhighlight %}

Once this is done, we can also declare our foreign keys explicitly.

{% highlight sql %}
ALTER TABLE dbo.InternalData
ADD CONSTRAINT FK_InternalData_ExternalSourceData_ExternalIdNumber
FOREIGN KEY (ExternalIdNumber)
REFERENCES dbo.ExternalSourceData(Id);
{% endhighlight %}

With the foreign key in place, re-executing our original query SQL Server no longer even needs to touch the `ExternalSourceData` table:

`Table 'InternalData'. Scan count 1, logical reads 19`

![Foreign Key Query](/assets/2023-12-27-implicit-conversions/05-foreign-key.png)