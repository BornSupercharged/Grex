using System;
using System.Reflection;
using Microsoft.UI.Xaml.Input;
using Grex.Controls;
using Xunit;

namespace Grex.Tests
{
    public class SearchTabContentRightClickTests
    {
        [Fact]
        public void SearchTabContent_ShouldHaveResultsListViewRightTappedMethod()
        {
            // Arrange & Act
            var rightTappedMethod = typeof(SearchTabContent).GetMethod("ResultsListView_RightTapped", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Assert
            Assert.NotNull(rightTappedMethod);
            
            // Verify that method signature is correct
            var parameters = rightTappedMethod.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(object), parameters[0].ParameterType);
            Assert.Equal(typeof(RightTappedRoutedEventArgs), parameters[1].ParameterType);
        }
        
        [Fact]
        public void SearchTabContent_ShouldHaveFilesResultsListViewRightTappedMethod()
        {
            // Arrange & Act
            var rightTappedMethod = typeof(SearchTabContent).GetMethod("FilesResultsListView_RightTapped", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Assert
            Assert.NotNull(rightTappedMethod);
            
            // Verify that method signature is correct
            var parameters = rightTappedMethod.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(object), parameters[0].ParameterType);
            Assert.Equal(typeof(RightTappedRoutedEventArgs), parameters[1].ParameterType);
        }
        
        [Fact]
        public void SearchTabContent_ShouldHaveContextMenuServiceField()
        {
            // Arrange & Act
            var contextMenuServiceField = typeof(SearchTabContent).GetField("_contextMenuService", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Assert
            Assert.NotNull(contextMenuServiceField);
            Assert.Equal(typeof(Grex.Services.ContextMenuService), contextMenuServiceField.FieldType);
        }
    }
}