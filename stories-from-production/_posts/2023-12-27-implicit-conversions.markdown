---
layout: post
title:  "Microsoft SQL Server Implicit Conversions"
date:   2023-12-27 18:51:40 +0000
categories: sql server
---

## Implicit Conversions in Microsoft SQL Server

A very common issue is an implicit conversion. They occur when SQL Server is told to compare two values of differing data types.
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

In this situation the value that is being filtered by is a type mismatch to the data type in the table.
In the code snippet below, a lookup is performed for the ExternalId value `156` as an integer. Examining the plan generated, a warning is produced about an implicit conversion and an index scan is performed on IX_Righty_ExternalId.

If `SET STATISTICS IO ON;` is also set it's observable that a large number of logical reads is performed, in this case just over 18,000.

{% highlight sql %}
SET STATISTICS IO ON;

SELECT Id, ExternalId, ExternalIdNumber
FROM Righty r
WHERE r.ExternalId = 156
{% endhighlight% }

`Table 'Righty'. Scan count 11, logical reads 18002`

![Where Clause implicit conversion](/assets/2023-12-27-implicit-conversions/01-where-clause.png)

### Solution

Simply replace the integer in the where clause with a data type that matches the value in the table, in this case a varchar(100).

{% highlight sql %}
SELECT Id, ExternalId, ExternalIdNumber
FROM Righty r
WHERE r.ExternalId = '156'
{% endhighlight %}

`Table 'Righty'. Scan count 1, logical reads 6`

![Where Clause implicit conversion](../assets/2023-12-27-implicit-conversions/02-where-clause-solution.png)

## Implicit Conversion Caused by Joining on Two Different Datatypes

Check out the [Jekyll docs][jekyll-docs] for more info on how to get the most out of Jekyll. File all bugs/feature requests at [Jekyll’s GitHub repo][jekyll-gh]. If you have questions, you can ask them on [Jekyll Talk][jekyll-talk].

[jekyll-docs]: https://jekyllrb.com/docs/home
[jekyll-gh]:   https://github.com/jekyll/jekyll
[jekyll-talk]: https://talk.jekyllrb.com/
