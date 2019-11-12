﻿using Com.Bateeq.Service.Merchandiser.Lib;
using Com.Bateeq.Service.Merchandiser.Lib.Models;
using Com.Bateeq.Service.Merchandiser.Lib.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Com.Bateeq.Service.Merchandiser.Test.DataUtils
{
    public class CategoryServiceDataUtil
    {
        public MerchandiserDbContext DbContext { get; set; }

        public CategoryService CategoryService { get; set; }

        public CategoryServiceDataUtil(MerchandiserDbContext dbContext, CategoryService categoryService)
        {
            this.DbContext = dbContext;
            this.CategoryService = categoryService;
            this.CategoryService.Username = "unit test";
        }
        
        public Task<Category> GetTestCategory()
        {
            Category testCategory = CategoryService.DbSet.FirstOrDefault(category => category.Code == "Test");

            if (testCategory != null)
                return Task.FromResult(testCategory);
            else
            {
                testCategory = new Category()
                {
                    Code = "Test",
                    Name = "Test Category",
                    SubCategory = "Test Category Sub Category"
                };
                CategoryService.Create(testCategory);
                return CategoryService.GetAsync(testCategory.Id);
            }
        }

        public Task<Category> GetTestCategory_Fabric()
        {
            Category testCategory = CategoryService.DbSet.FirstOrDefault(category => category.Code == "Test Fabric");

            if (testCategory != null)
                return Task.FromResult(testCategory);
            else
            {
                testCategory = new Category()
                {
                    Code = "Test Fabric",
                    Name = "Fabric",
                    SubCategory = "Test Category Sub Category"
                };
                CategoryService.Create(testCategory);
                return CategoryService.GetAsync(testCategory.Id);
            }
        }
    }
}
