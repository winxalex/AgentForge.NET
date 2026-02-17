--1️⃣ Креирај metadata табела
DROP TABLE IF EXISTS ExtendedProperties;

CREATE TABLE
    ExtendedProperties (
        ObjectType TEXT NOT NULL, -- table | view | column
        ObjectName TEXT NOT NULL, -- table/view name
        ColumnName TEXT, -- NULL за table/view
        PropertyName TEXT NOT NULL, -- 'Description'
        PropertyValue TEXT,
        PRIMARY KEY (ObjectType, ObjectName, ColumnName, PropertyName)
    );

--2️⃣ Insert за сите Tables и Views
-- INSERT INTO
--     ExtendedProperties (
--         ObjectType,
--         ObjectName,
--         PropertyName,
--         PropertyValue
--     )
-- SELECT
--     type AS ObjectType,
--     name AS ObjectName,
--     'Description' AS PropertyName,
--     'TODO: Add description here' AS PropertyValue
-- FROM
--     sqlite_master
-- WHERE
--     type IN ('table', 'view')
--     AND name NOT LIKE 'sqlite_%';

-- --Ова ќе направи по еден Description ред за секоја табела и view.
-- --3️⃣ Ако сакаш auto-description (пример)
-- INSERT INTO
--     ExtendedProperties (
--         ObjectType,
--         ObjectName,
--         PropertyName,
--         PropertyValue
--     )
-- SELECT
--     type,
--     name,
--     'Description',
--     'Auto generated description for ' || type || ' ' || name
-- FROM
--     sqlite_master
-- WHERE
--     type IN ('table', 'view')
--     AND name NOT LIKE 'sqlite_%';

--4️⃣ Пример како да читаш Description
--SELECT
--    m.type,
--    m.name,
--    ep.PropertyValue AS Description
--FROM
--    sqlite_master m
--    LEFT JOIN ExtendedProperties ep ON ep.ObjectName = m.name
--    AND ep.PropertyName = 'Description'
--WHERE
--    m.type IN ('table', 'view')
--    AND m.name NOT LIKE 'sqlite_%';

--2️⃣ TABLE DESCRIPTIONS
INSERT INTO
    ExtendedProperties
VALUES
    (
        'table',
        'Categories',
        NULL,
        'Description',
        'Stores product category definitions used to group products.'
    ),
    (
        'table',
        'Customers',
        NULL,
        'Description',
        'Stores customer master data including company and contact information.'
    ),
    (
        'table',
        'Employees',
        NULL,
        'Description',
        'Stores employee records and organizational hierarchy information.'
    ),
    (
        'table',
        'Order Details',
        NULL,
        'Description',
        'Stores line items for each order including product, quantity and pricing.'
    ),
    (
        'table',
        'Orders',
        NULL,
        'Description',
        'Stores customer order headers including shipping and billing information.'
    ),
    (
        'table',
        'Products',
        NULL,
        'Description',
        'Stores product catalog including pricing and supplier references.'
    ),
    (
        'table',
        'Shippers',
        NULL,
        'Description',
        'Stores shipping companies used to deliver orders.'
    ),
    (
        'table',
        'Suppliers',
        NULL,
        'Description',
        'Stores supplier companies providing products.'
    ),
    (
        'table',
        'Territories',
        NULL,
        'Description',
        'Stores sales territories assigned to employees.'
    ),
    (
        'table',
        'Region',
        NULL,
        'Description',
        'Stores high level sales regions grouping territories.'
    );

--3️⃣ COLUMN DESCRIPTIONS (пример за главните табели)
--Products
INSERT INTO
    ExtendedProperties
VALUES
    (
        'column',
        'Products',
        'ProductID',
        'Description',
        'Primary key of the product.'
    ),
    (
        'column',
        'Products',
        'ProductName',
        'Description',
        'Name of the product.'
    ),
    (
        'column',
        'Products',
        'SupplierID',
        'Description',
        'Reference to supplier providing the product.'
    ),
    (
        'column',
        'Products',
        'CategoryID',
        'Description',
        'Reference to product category.'
    ),
    (
        'column',
        'Products',
        'QuantityPerUnit',
        'Description',
        'Packaging description of the product.'
    ),
    (
        'column',
        'Products',
        'UnitPrice',
        'Description',
        'Selling price per unit.'
    ),
    (
        'column',
        'Products',
        'UnitsInStock',
        'Description',
        'Current available stock quantity.'
    ),
    (
        'column',
        'Products',
        'UnitsOnOrder',
        'Description',
        'Quantity currently on purchase order.'
    ),
    (
        'column',
        'Products',
        'ReorderLevel',
        'Description',
        'Minimum stock level before reorder is triggered.'
    ),
    (
        'column',
        'Products',
        'Discontinued',
        'Description',
        'Indicates whether the product is discontinued.'
    );

--Orders
INSERT INTO
    ExtendedProperties
VALUES
    (
        'column',
        'Orders',
        'OrderID',
        'Description',
        'Primary key of the order.'
    ),
    (
        'column',
        'Orders',
        'CustomerID',
        'Description',
        'Reference to customer placing the order.'
    ),
    (
        'column',
        'Orders',
        'EmployeeID',
        'Description',
        'Employee responsible for the order.'
    ),
    (
        'column',
        'Orders',
        'OrderDate',
        'Description',
        'Date when the order was placed.'
    ),
    (
        'column',
        'Orders',
        'RequiredDate',
        'Description',
        'Date when the order is required by the customer.'
    ),
    (
        'column',
        'Orders',
        'ShippedDate',
        'Description',
        'Date when the order was shipped.'
    ),
    (
        'column',
        'Orders',
        'ShipVia',
        'Description',
        'Shipping company used for the order.'
    ),
    (
        'column',
        'Orders',
        'Freight',
        'Description',
        'Shipping cost charged for the order.'
    ),
    (
        'column',
        'Orders',
        'ShipName',
        'Description',
        'Shipping recipient name.'
    ),
    (
        'column',
        'Orders',
        'ShipCity',
        'Description',
        'City where the order is shipped.'
    );

--Customers
INSERT INTO
    ExtendedProperties
VALUES
    (
        'column',
        'Customers',
        'CustomerID',
        'Description',
        'Primary key of the customer.'
    ),
    (
        'column',
        'Customers',
        'CompanyName',
        'Description',
        'Official company name of the customer.'
    ),
    (
        'column',
        'Customers',
        'ContactName',
        'Description',
        'Primary contact person.'
    ),
    (
        'column',
        'Customers',
        'ContactTitle',
        'Description',
        'Job title of the contact person.'
    ),
    (
        'column',
        'Customers',
        'City',
        'Description',
        'City where the customer is located.'
    ),
    (
        'column',
        'Customers',
        'Country',
        'Description',
        'Country where the customer operates.'
    );

--🟢 Categories
INSERT INTO ExtendedProperties VALUES
('column','Categories','CategoryID','Description','Primary key of the category.'),
('column','Categories','CategoryName','Description','Name of the product category.'),
('column','Categories','Description','Description','Textual description of the category.'),
('column','Categories','Picture','Description','Optional image representing the category.');

--🟢 Suppliers
INSERT INTO ExtendedProperties VALUES
('column','Suppliers','SupplierID','Description','Primary key of the supplier.'),
('column','Suppliers','CompanyName','Description','Official supplier company name.'),
('column','Suppliers','ContactName','Description','Primary contact person at supplier.'),
('column','Suppliers','ContactTitle','Description','Job title of supplier contact.'),
('column','Suppliers','Address','Description','Street address of the supplier.'),
('column','Suppliers','City','Description','City where the supplier is located.'),
('column','Suppliers','Region','Description','Region or state of the supplier.'),
('column','Suppliers','PostalCode','Description','Postal code of the supplier.'),
('column','Suppliers','Country','Description','Country where the supplier operates.'),
('column','Suppliers','Phone','Description','Supplier phone number.'),
('column','Suppliers','Fax','Description','Supplier fax number.'),
('column','Suppliers','HomePage','Description','Supplier website or homepage.');

--🟢 Shippers
INSERT INTO ExtendedProperties VALUES
('column','Shippers','ShipperID','Description','Primary key of the shipper.'),
('column','Shippers','CompanyName','Description','Shipping company name.'),
('column','Shippers','Phone','Description','Contact phone number of the shipper.');

--🟢 Order Details
INSERT INTO ExtendedProperties VALUES
('column','Order Details','OrderID','Description','Reference to the order header.'),
('column','Order Details','ProductID','Description','Reference to the ordered product.'),
('column','Order Details','UnitPrice','Description','Unit price at the time of order.'),
('column','Order Details','Quantity','Description','Quantity of the product ordered.'),
('column','Order Details','Discount','Description','Discount percentage applied to the line item.');

--🟢 Employees
INSERT INTO ExtendedProperties VALUES
('column','Employees','EmployeeID','Description','Primary key of the employee.'),
('column','Employees','LastName','Description','Employee last name.'),
('column','Employees','FirstName','Description','Employee first name.'),
('column','Employees','Title','Description','Employee job title.'),
('column','Employees','TitleOfCourtesy','Description','Courtesy title (e.g., Mr., Ms., Dr.).'),
('column','Employees','BirthDate','Description','Employee date of birth.'),
('column','Employees','HireDate','Description','Date when the employee was hired.'),
('column','Employees','Address','Description','Employee street address.'),
('column','Employees','City','Description','City of residence.'),
('column','Employees','Region','Description','Region or state of residence.'),
('column','Employees','PostalCode','Description','Postal code of residence.'),
('column','Employees','Country','Description','Country of residence.'),
('column','Employees','HomePhone','Description','Employee home phone number.'),
('column','Employees','Extension','Description','Internal phone extension number.'),
('column','Employees','Photo','Description','Employee photo.'),
('column','Employees','Notes','Description','Additional notes about the employee.'),
('column','Employees','ReportsTo','Description','Manager employee ID reference.'),
('column','Employees','PhotoPath','Description','File path to employee photo.');

--🟢 Territories
INSERT INTO ExtendedProperties VALUES
('column','Territories','TerritoryID','Description','Primary key of the territory.'),
('column','Territories','TerritoryDescription','Description','Name or description of the territory.'),
('column','Territories','RegionID','Description','Reference to the region.');

--🟢 Region
INSERT INTO ExtendedProperties VALUES
('column','Region','RegionID','Description','Primary key of the region.'),
('column','Region','RegionDescription','Description','Name or description of the region.');

--🟢 EmployeeTerritories (ако ја имаш во твојата верзија)
INSERT INTO ExtendedProperties VALUES
('column','EmployeeTerritories','EmployeeID','Description','Reference to employee assigned to territory.'),
('column','EmployeeTerritories','TerritoryID','Description','Reference to assigned territory.');



--4️⃣ VIEW DESCRIPTIONS (според твојата листа)
INSERT INTO
    ExtendedProperties
VALUES
    (
        'view',
        'Alphabetical list of Products',
        NULL,
        'Description',
        'Displays all products sorted alphabetically by product name.'
    ),
    (
        'view',
        'Current Product List',
        NULL,
        'Description',
        'Shows active products that are not discontinued.'
    ),
    (
        'view',
        'Customer and Suppliers by City',
        NULL,
        'Description',
        'Combines customers and suppliers grouped by city.'
    ),
    (
        'view',
        'CustOrderHist',
        NULL,
        'Description',
        'Shows total quantities of products ordered by a specific customer.'
    ),
    (
        'view',
        'CustOrdersOrders',
        NULL,
        'Description',
        'Returns orders placed by a specific customer including order details.'
    ),
    (
        'view',
        'Employee Sales by Country',
        NULL,
        'Description',
        'Summarizes employee sales totals grouped by country.'
    ),
    (
        'view',
        'Invoices',
        NULL,
        'Description',
        'Provides detailed invoice-level sales information including totals.'
    ),
    (
        'view',
        'Orders Qry',
        NULL,
        'Description',
        'Extended order information including customer and shipping data.'
    ),
    (
        'view',
        'Product Sales for 1997',
        NULL,
        'Description',
        'Shows total sales per product for the year 1997.'
    ),
    (
        'view',
        'Products Above Average Price',
        NULL,
        'Description',
        'Lists products with unit price higher than average product price.'
    ),
    (
        'view',
        'Products by Category',
        NULL,
        'Description',
        'Displays products grouped by category.'
    ),
    (
        'view',
        'Quarterly Orders',
        NULL,
        'Description',
        'Shows order totals aggregated by quarter.'
    ),
    (
        'view',
        'Sales by Category',
        NULL,
        'Description',
        'Summarizes sales totals grouped by product category.'
    ),
    (
        'view',
        'Sales Totals by Amount',
        NULL,
        'Description',
        'Displays orders filtered by sales amount thresholds.'
    ),
    (
        'view',
        'Summary of Sales by Quarter',
        NULL,
        'Description',
        'Aggregated sales totals per quarter.'
    ),
    (
        'view',
        'Summary of Sales by Year',
        NULL,
        'Description',
        'Aggregated sales totals per year.'
    );





--🟦 Alphabetical list of Products
INSERT INTO ExtendedProperties VALUES
('column','Alphabetical list of Products','ProductID','Description','Unique product identifier.'),
('column','Alphabetical list of Products','ProductName','Description','Product name sorted alphabetically.'),
('column','Alphabetical list of Products','SupplierID','Description','Supplier reference.'),
('column','Alphabetical list of Products','CategoryID','Description','Category reference.'),
('column','Alphabetical list of Products','QuantityPerUnit','Description','Packaging description.'),
('column','Alphabetical list of Products','UnitPrice','Description','Current selling price.'),
('column','Alphabetical list of Products','UnitsInStock','Description','Available stock quantity.'),
('column','Alphabetical list of Products','Discontinued','Description','Indicates whether product is discontinued.');

--🟦 Current Product List
INSERT INTO ExtendedProperties VALUES
('column','Current Product List','ProductID','Description','Unique product identifier.'),
('column','Current Product List','ProductName','Description','Active product name.'),
('column','Current Product List','QuantityPerUnit','Description','Packaging description.'),
('column','Current Product List','UnitPrice','Description','Current selling price.');

--🟦 Customer and Suppliers by City
INSERT INTO ExtendedProperties VALUES
('column','Customer and Suppliers by City','City','Description','City name.'),
('column','Customer and Suppliers by City','CompanyName','Description','Customer or supplier company name.'),
('column','Customer and Suppliers by City','ContactName','Description','Primary contact person.'),
('column','Customer and Suppliers by City','Relationship','Description','Indicates whether company is Customer or Supplier.');

--🟦 CustOrderHist
INSERT INTO ExtendedProperties VALUES
('column','CustOrderHist','ProductName','Description','Product ordered by customer.'),
('column','CustOrderHist','Total','Description','Total quantity ordered by the customer for the product.');

--🟦 CustOrdersOrders
INSERT INTO ExtendedProperties VALUES
('column','CustOrdersOrders','OrderID','Description','Unique order identifier.'),
('column','CustOrdersOrders','OrderDate','Description','Date the order was placed.'),
('column','CustOrdersOrders','RequiredDate','Description','Date order is required.'),
('column','CustOrdersOrders','ShippedDate','Description','Date order was shipped.');

--🟦 Employee Sales by Country
INSERT INTO ExtendedProperties VALUES
('column','Employee Sales by Country','Country','Description','Country where sales were made.'),
('column','Employee Sales by Country','LastName','Description','Employee last name.'),
('column','Employee Sales by Country','FirstName','Description','Employee first name.'),
('column','Employee Sales by Country','ShippedDate','Description','Shipment date.'),
('column','Employee Sales by Country','OrderID','Description','Order reference.'),
('column','Employee Sales by Country','SaleAmount','Description','Total sales amount for the order.');

--🟦 Invoices
INSERT INTO ExtendedProperties VALUES
('column','Invoices','ShipName','Description','Recipient name.'),
('column','Invoices','ShipAddress','Description','Shipping address.'),
('column','Invoices','ShipCity','Description','Shipping city.'),
('column','Invoices','CustomerID','Description','Customer reference.'),
('column','Invoices','CustomerName','Description','Customer company name.'),
('column','Invoices','Salesperson','Description','Employee responsible for the sale.'),
('column','Invoices','OrderID','Description','Order reference.'),
('column','Invoices','OrderDate','Description','Date order was placed.'),
('column','Invoices','ProductID','Description','Product reference.'),
('column','Invoices','ProductName','Description','Product name.'),
('column','Invoices','UnitPrice','Description','Price per unit.'),
('column','Invoices','Quantity','Description','Quantity sold.'),
('column','Invoices','Discount','Description','Discount applied.'),
('column','Invoices','ExtendedPrice','Description','Calculated line total after discount.');

--🟦 Orders Qry
INSERT INTO ExtendedProperties VALUES
('column','Orders Qry','OrderID','Description','Unique order identifier.'),
('column','Orders Qry','CustomerID','Description','Customer reference.'),
('column','Orders Qry','EmployeeID','Description','Employee responsible.'),
('column','Orders Qry','OrderDate','Description','Date order was placed.'),
('column','Orders Qry','RequiredDate','Description','Date order is required.'),
('column','Orders Qry','ShippedDate','Description','Date order was shipped.'),
('column','Orders Qry','ShipVia','Description','Shipping method.'),
('column','Orders Qry','Freight','Description','Shipping cost.');

--🟦 Product Sales for 1997
INSERT INTO ExtendedProperties VALUES
('column','Product Sales for 1997','CategoryName','Description','Product category.'),
('column','Product Sales for 1997','ProductName','Description','Product name.'),
('column','Product Sales for 1997','ProductSales','Description','Total sales amount for 1997.');

--🟦 Products Above Average Price
INSERT INTO ExtendedProperties VALUES
('column','Products Above Average Price','ProductName','Description','Product name.'),
('column','Products Above Average Price','UnitPrice','Description','Product unit price higher than average.');

--🟦 Products by Category
INSERT INTO ExtendedProperties VALUES
('column','Products by Category','CategoryName','Description','Product category name.'),
('column','Products by Category','ProductName','Description','Product name.'),
('column','Products by Category','QuantityPerUnit','Description','Packaging description.'),
('column','Products by Category','UnitsInStock','Description','Available stock quantity.'),
('column','Products by Category','Discontinued','Description','Indicates whether product is discontinued.');

--🟦 Quarterly Orders
INSERT INTO ExtendedProperties VALUES
('column','Quarterly Orders','CustomerID','Description','Customer reference.'),
('column','Quarterly Orders','CompanyName','Description','Customer company name.'),
('column','Quarterly Orders','OrderID','Description','Order reference.'),
('column','Quarterly Orders','OrderDate','Description','Order date.'),
('column','Quarterly Orders','ShippedDate','Description','Shipment date.');

--🟦 Sales by Category
INSERT INTO ExtendedProperties VALUES
('column','Sales by Category','CategoryID','Description','Category reference.'),
('column','Sales by Category','CategoryName','Description','Category name.'),
('column','Sales by Category','ProductName','Description','Product name.'),
('column','Sales by Category','ProductSales','Description','Total sales amount per product.');

---🟦 Sales Totals by Amount
INSERT INTO ExtendedProperties VALUES
('column','Sales Totals by Amount','SaleAmount','Description','Total order sales amount.'),
('column','Sales Totals by Amount','OrderID','Description','Order reference.'),
('column','Sales Totals by Amount','CompanyName','Description','Customer company name.'),
('column','Sales Totals by Amount','ShippedDate','Description','Shipment date.');

--🟦 Summary of Sales by Quarter
INSERT INTO ExtendedProperties VALUES
('column','Summary of Sales by Quarter','ShippedDate','Description','Shipment date grouped by quarter.'),
('column','Summary of Sales by Quarter','Subtotal','Description','Total sales amount per quarter.');

--🟦 Summary of Sales by Year
INSERT INTO ExtendedProperties VALUES
('column','Summary of Sales by Year','ShippedDate','Description','Shipment date grouped by year.'),
('column','Summary of Sales by Year','Subtotal','Description','Total sales amount per year.');


INSERT INTO ExtendedProperties VALUES
('column','Customers','Region','Embed','true'),
('column','Suppliers','Region','Embed','true'),
('column','Employees','Region','Embed','true'),
('column','Orders','ShipRegion','Embed','true');


INSERT INTO ExtendedProperties VALUES
('table','Customers',NULL,'Domain','Sales'),

('table','Orders',NULL,'Domain','Sales'),
('table','Order Details',NULL,'Domain','Sales'),

('table','Products',NULL,'Domain','Inventory'),
('table','Categories',NULL,'Domain','Inventory'),

('table','Suppliers',NULL,'Domain','Procurement'),

('table','Shippers',NULL,'Domain','Logistics'),

('table','Employees',NULL,'Domain','HR,Sales'),

('table','Territories',NULL,'Domain','Sales'),
('table','Region',NULL,'Domain','Sales');



--5️⃣ Query за преглед на сè
SELECT
    *
FROM
    ExtendedProperties
ORDER BY
    ObjectType,
    ObjectName,
    ColumnName;