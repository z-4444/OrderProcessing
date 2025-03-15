using Xunit;
using OrderProcessing.Controllers;
using OrderProcessing.Models;
using Microsoft.AspNetCore.Mvc;

namespace OrderProcessing.Tests
{
    public class OrderTests
    {
        [Fact]
        public void Index_ReturnsViewResult_WithEmptyOrder()
        {
            // Arrange
            var controller = new OrderController();

            // Act
            var result = controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<Order>(viewResult.Model);
            Assert.Null(model.CustomerType); // Should be empty initially
            Assert.Equal(0, model.Amount);
        }

        [Fact]
        public void Result_ValidOrder_ReturnsViewResult_WithOrder()
        {
            // Arrange
            var controller = new OrderController();
            var order = new Order { Amount = 100, CustomerType = "Loyal" };

            // Act
            var result = controller.Result(order);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<Order>(viewResult.Model);
            Assert.Equal(100, model.Amount);
            Assert.Equal("Loyal", model.CustomerType);
        }

        [Fact]
        public void Result_InvalidOrder_ReturnsIndexView()
        {
            // Arrange
            var controller = new OrderController();
            controller.ModelState.AddModelError("OrderAmount", "Required");
            var order = new Order { CustomerType = "New" };

            // Act
            var result = controller.Result(order);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            var model = Assert.IsAssignableFrom<Order>(viewResult.Model);
            Assert.Equal("New", model.CustomerType);
        }
    }
}