using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Data.SqlClient;

namespace WarehouseManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WarehouseController : ControllerBase
    {
        private readonly string _connectionString = "Server=db-mssql.pjwstk.edu.pl;Database=s25059;User Id=s25059;Password=;TrustServerCertificate=True;";


        public WarehouseController(IConfiguration configuration)
        {
        }

        [HttpPost("add-product")]
        public IActionResult AddProduct([FromBody] AddProductRequest request)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                bool productExists = CheckProductExists(connection, request.ProductId);
                bool warehouseExists = CheckWarehouseExists(connection, request.WarehouseId);

                if (!productExists || !warehouseExists || request.Amount <= 0)
                {
                    return BadRequest("Invalid product, warehouse or amount.");
                }

                bool orderValid = CheckOrder(connection, request.ProductId, request.Amount, request.CreatedAt);

                if (!orderValid)
                {
                    return BadRequest("Invalid order data or order has been fulfilled.");
                }

                try
                {
                    connection.Open();

                    UpdateOrderFulfilledAt(connection, request.ProductId, request.CreatedAt, DateTime.Now);

                    int newRecordId = InsertProductToWarehouse(connection, request.WarehouseId, request.ProductId, request.Amount);

                    return Ok(newRecordId);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, ex.Message);
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        private bool CheckProductExists(SqlConnection connection, int productId)
        {
            string query = "SELECT COUNT(1) FROM Product WHERE Id = @ProductId";
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ProductId", productId);
                connection.Open();
                int count = (int)command.ExecuteScalar();
                connection.Close();
                return count > 0;
            }
        }

        private bool CheckWarehouseExists(SqlConnection connection, int warehouseId)
        {
            string query = "SELECT COUNT(1) FROM Warehouse WHERE Id = @WarehouseId";
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@WarehouseId", warehouseId);
                connection.Open();
                int count = (int)command.ExecuteScalar();
                connection.Close();
                return count > 0;
            }
        }

        private bool CheckOrder(SqlConnection connection, int productId, int amount, DateTime createdAt)
        {
            string query = @"
                SELECT COUNT(1) 
                FROM [Order]
                WHERE ProductId = @ProductId
                AND Amount = @Amount
                AND CreatedAt < @CreatedAt
                AND NOT EXISTS (
                    SELECT 1 
                    FROM Product_Warehouse 
                    WHERE Product_Warehouse.OrderId = [Order].Id
                )";
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ProductId", productId);
                command.Parameters.AddWithValue("@Amount", amount);
                command.Parameters.AddWithValue("@CreatedAt", createdAt);
                connection.Open();
                int count = (int)command.ExecuteScalar();
                connection.Close();
                return count > 0;
            }
        }

        private void UpdateOrderFulfilledAt(SqlConnection connection, int productId, DateTime createdAt, DateTime fulfilledAt)
        {
            string updateQuery = @"
                UPDATE [Order]
                SET FulfilledAt = @FulfilledAt
                WHERE Id = (
                    SELECT TOP 1 Id 
                    FROM [Order]
                    WHERE ProductId = @ProductId
                    AND FulfilledAt IS NULL
                    ORDER BY CreatedAt ASC
                )";
            using (SqlCommand command = new SqlCommand(updateQuery, connection))
            {
                command.Parameters.AddWithValue("@ProductId", productId);
                command.Parameters.AddWithValue("@FulfilledAt", fulfilledAt);
                connection.Open();
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        private int InsertProductToWarehouse(SqlConnection connection, int warehouseId, int productId, int amount)
        {
            string insertQuery = @"
                INSERT INTO Product_Warehouse (WarehouseId, ProductId, Amount, Price, CreatedAt)
                VALUES (@WarehouseId, @ProductId, @Amount, 
                        (SELECT Price FROM Product WHERE Id = @ProductId) * @Amount, GETDATE());
                SELECT SCOPE_IDENTITY();";
            using (SqlCommand command = new SqlCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@WarehouseId", warehouseId);
                command.Parameters.AddWithValue("@ProductId", productId);
                command.Parameters.AddWithValue("@Amount", amount);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }

    public class AddProductRequest
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
