﻿using Com.Bateeq.Service.Merchandiser.Lib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Com.Bateeq.Service.Merchandiser.Lib.Helpers;
using System.Linq.Dynamic.Core;
using Microsoft.Extensions.DependencyInjection;
using Com.Moonlay.NetCore.Lib;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Com.Bateeq.Service.Merchandiser.Lib.Interfaces;
using Com.Bateeq.Service.Merchandiser.Lib.ViewModels;
using Com.Bateeq.Service.Merchandiser.Lib.Services.AzureStorage;
using Com.Bateeq.Service.Merchandiser.Lib.Exceptions;

namespace Com.Bateeq.Service.Merchandiser.Lib.Services
{
    public class CostCalculationRetailService : BasicService<MerchandiserDbContext, CostCalculationRetail>, IMap<CostCalculationRetail, CostCalculationRetailViewModel>
    {
        public CostCalculationRetailService(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
        private AzureImageService AzureImageService
        {
            get { return this.ServiceProvider.GetService<AzureImageService>(); }
        }

        private CostCalculationRetail_MaterialService CostCalculationRetail_MaterialService
        {
            get
            {
                CostCalculationRetail_MaterialService service = this.ServiceProvider.GetService<CostCalculationRetail_MaterialService>();
                service.Username = this.Username;
                return service;
            }
        }

        private SizeRangeService SizeRangeService
        {
            get
            {
                SizeRangeService service = this.ServiceProvider.GetService<SizeRangeService>();
                service.Username = this.Username;
                return service;
            }
        }

        public override Tuple<List<CostCalculationRetail>, int, Dictionary<string, string>, List<string>> ReadModel(int Page = 1, int Size = 25, string Order = "{}", List<string> Select = null, string Keyword = null, string Filter = "{}")
        {
            IQueryable<CostCalculationRetail> Query = this.DbContext.CostCalculationRetails;

            List<string> SearchAttributes = new List<string>()
                {
                    "Article", "RO"
                };
            Query = ConfigureSearch(Query, SearchAttributes, Keyword);

            Dictionary<string, object> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(Filter);
            Query = ConfigureFilter(Query, FilterDictionary);

            List<string> SelectedFields = new List<string>()
                {
                    "Id", "Code", "RO", "Article", "Style", "Counter"
                };
            Query = Query
                .Select(ccr => new CostCalculationRetail
                {
                    Id = ccr.Id,
                    Code = ccr.Code,
                    RO = ccr.RO,
                    Article = ccr.Article,
                    StyleId = ccr.StyleId,
                    StyleName = ccr.StyleName,
                    CounterId = ccr.StyleId,
                    CounterName = ccr.CounterName
                });

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            Query = ConfigureOrder(Query, OrderDictionary);

            Pageable<CostCalculationRetail> pageable = new Pageable<CostCalculationRetail>(Query, Page - 1, Size);
            List<CostCalculationRetail> Data = pageable.Data.ToList<CostCalculationRetail>();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary, SelectedFields);
        }

        public override async Task<int> CreateModel(CostCalculationRetail Model)
        {
            int latestSN = this.DbSet
                .Where(d => d.SeasonId.Equals(Model.SeasonId))
                .DefaultIfEmpty()
                .Max(d => d.RO_SerialNumber);
            Model.RO_SerialNumber = latestSN != 0 ? latestSN + 1 : 1;
            Model.RO = String.Format("{0}{1:D4}", Model.SeasonCode, Model.RO_SerialNumber);
            int created = await this.CreateAsync(Model);
            
            Model.ImagePath = await this.AzureImageService.UploadImage(Model.GetType().Name, Model.Id, Model._CreatedUtc, Model.ImageFile);
            await this.UpdateAsync(Model.Id, Model);

            return created;
        }

        public override async Task<CostCalculationRetail> ReadModelById(int id)
        {
            CostCalculationRetail read = await this.DbSet
                .Where(d => d.Id.Equals(id) && d._IsDeleted.Equals(false))
                .Include(d => d.CostCalculationRetail_Materials)
                .FirstOrDefaultAsync();

            read.CostCalculationRetail_Materials = read.CostCalculationRetail_Materials.OrderByDescending(material => material.Total).ToList();

            read.ImageFile = await this.AzureImageService.DownloadImage(read.GetType().Name, read.ImagePath);

            return read;
        }

        public override async Task<int> UpdateModel(int Id, CostCalculationRetail Model)
        {
            Model.ImagePath = await this.AzureImageService.UploadImage(Model.GetType().Name, Model.Id, Model._CreatedUtc, Model.ImageFile);

            int updated = await this.UpdateAsync(Id, Model);

            if (Model.CostCalculationRetail_Materials != null)
            {
                HashSet<int> costCalculationRetail_Materials = new HashSet<int>(this.CostCalculationRetail_MaterialService.DbSet
                    .Where(p => p.CostCalculationRetailId.Equals(Id))
                    .Select(p => p.Id));

                foreach (int costCalculationRetail_Material in costCalculationRetail_Materials)
                {
                    CostCalculationRetail_Material model = Model.CostCalculationRetail_Materials.FirstOrDefault(prop => prop.Id.Equals(costCalculationRetail_Material));

                    if (model == null)
                    {
                        await this.CostCalculationRetail_MaterialService.DeleteModel(costCalculationRetail_Material);
                    }
                    else
                    {
                        await this.CostCalculationRetail_MaterialService.UpdateModel(costCalculationRetail_Material, model);
                    }
                }

                foreach (CostCalculationRetail_Material costCalculationRetail_Material in Model.CostCalculationRetail_Materials)
                {
                    if (costCalculationRetail_Material.Id.Equals(0))
                    {
                        await this.CostCalculationRetail_MaterialService.CreateModel(costCalculationRetail_Material);
                    }
                }
            }

            return updated;
        }

        public override async Task<int> DeleteModel(int Id)
        {
            CostCalculationRetail deleted = await this.GetAsync(Id);

            if (deleted.RO_RetailId != null)
            {
                throw new DbReferenceNotNullException("Cost Calculation Retail ini tidak bisa di delete karena masih terdaftar di RO");
            }
            else
            {
                HashSet<int> costCalculationRetail_Materials = new HashSet<int>(this.CostCalculationRetail_MaterialService.DbSet
                    .Where(p => p.CostCalculationRetailId.Equals(Id))
                    .Select(p => p.Id));
                foreach (int costCalculationRetail_Material in costCalculationRetail_Materials)
                {
                    await this.CostCalculationRetail_MaterialService.DeleteModel(costCalculationRetail_Material);
                }
                
                await this.AzureImageService.RemoveImage(deleted.GetType().Name, deleted.ImagePath);
            }

            return await this.DeleteAsync(Id);
        }

        public async Task GeneratePO(int Id)
        {
            HashSet<int> CostCalculationRetail_Materials = new HashSet<int>(this.CostCalculationRetail_MaterialService.DbSet
                    .Where(p => p.CostCalculationRetailId.Equals(Id))
                    .Select(p => p.Id));
            foreach (int id in CostCalculationRetail_Materials)
            {
                CostCalculationRetail_Material model = await CostCalculationRetail_MaterialService.ReadModelById(id);
                if (model.PO_SerialNumber == null || model.PO_SerialNumber == 0)
                {
                    await CostCalculationRetail_MaterialService.GeneratePO(model);
                }
            }
        }

        public override void OnCreating(CostCalculationRetail model)
        {
            do
            {
                model.Code = Code.Generate();
            }
            while (this.DbSet.Any(sr => sr.Code.Equals(model.Code)));

            if (model.CostCalculationRetail_Materials.Count > 0)
            {
                foreach (CostCalculationRetail_Material costCalculationRetail_Material in model.CostCalculationRetail_Materials)
                {
                    this.CostCalculationRetail_MaterialService.OnCreating(costCalculationRetail_Material);
                }
            }

            base.OnCreating(model);
        }

        public CostCalculationRetailViewModel MapToViewModel(CostCalculationRetail model)
        {
            CostCalculationRetailViewModel viewModel = new CostCalculationRetailViewModel();

            PropertyCopier<CostCalculationRetail, CostCalculationRetailViewModel>.Copy(model, viewModel);

            viewModel.Style = new ArticleStyleViewModel();
            viewModel.Style._id = model.StyleId;
            viewModel.Style.name = model.StyleName;

            viewModel.Season = new ArticleSeasonViewModel();
            viewModel.Season._id = model.SeasonId;
            viewModel.Season.name = model.SeasonName;

            viewModel.Buyer = new BuyerViewModel();
            viewModel.Buyer.Id = model.BuyerId;
            viewModel.Buyer.Name = model.BuyerName;

            viewModel.SizeRange = new SizeRangeViewModel();
            viewModel.SizeRange.Id = model.SizeRangeId;
            viewModel.SizeRange.Name = model.SizeRangeName;

            viewModel.FabricAllowance = Percentage.ToPercent(model.FabricAllowance);
            viewModel.AccessoriesAllowance = Percentage.ToPercent(model.AccessoriesAllowance);

            try
            {
                // Get Related Size of particular Size Range if possible
                viewModel.SizeRange.RelatedSizes = new List<RelatedSizeViewModel>();
                Task<SizeRange> sizeRange = this.SizeRangeService.ReadModelById(model.SizeRangeId);
                sizeRange.Wait();
                foreach (RelatedSize rs in sizeRange.Result.RelatedSizes)
                {
                    RelatedSizeViewModel relatedSizeVM = new RelatedSizeViewModel();
                    PropertyCopier<RelatedSize, RelatedSizeViewModel>.Copy(rs, relatedSizeVM);
                    SizeViewModel sizeVM = new SizeViewModel();
                    PropertyCopier<Size, SizeViewModel>.Copy(rs.Size, sizeVM);
                    relatedSizeVM.Size = sizeVM;
                    viewModel.SizeRange.RelatedSizes.Add(relatedSizeVM);
                }
            }
            catch (Exception)
            {
                // If cannot get Related Size, do nothing
            }

            viewModel.Counter = new ArticleCounterViewModel();
            viewModel.Counter._id = model.CounterId;
            viewModel.Counter.name = model.CounterName;

            viewModel.Efficiency = new EfficiencyViewModel();
            viewModel.Efficiency.Id = model.EfficiencyId;
            viewModel.Efficiency.Value = model.EfficiencyValue;

            viewModel.Risk = Percentage.ToPercent(model.Risk);

            viewModel.OL = new RateCalculatedViewModel();
            viewModel.OL.Id = model.OLId;
            viewModel.OL.Value = model.OLRate;
            viewModel.OL.CalculatedValue = model.OLCalculatedRate;

            viewModel.OTL1 = new RateCalculatedViewModel();
            viewModel.OTL1.Id = model.OTL1Id;
            viewModel.OTL1.Value = model.OTL1Rate;
            viewModel.OTL1.CalculatedValue = model.OTL1CalculatedRate;

            viewModel.OTL2 = new RateCalculatedViewModel();
            viewModel.OTL2.Id = model.OTL2Id;
            viewModel.OTL2.Value = model.OTL2Rate;
            viewModel.OTL2.CalculatedValue = model.OTL2CalculatedRate;

            viewModel.OTL3 = new RateCalculatedViewModel();
            viewModel.OTL3.Id = model.OTL3Id;
            viewModel.OTL3.Value = model.OTL3Rate;
            viewModel.OTL3.CalculatedValue = model.OTL3CalculatedRate;

            viewModel.CostCalculationRetail_Materials = new List<CostCalculationRetail_MaterialViewModel>();

            if (model.CostCalculationRetail_Materials != null)
            {
                foreach (CostCalculationRetail_Material costCalculationRetail_Material in model.CostCalculationRetail_Materials)
                {
                    CostCalculationRetail_MaterialViewModel costCalculationRetail_MaterialVM = new CostCalculationRetail_MaterialViewModel();
                    PropertyCopier<CostCalculationRetail_Material, CostCalculationRetail_MaterialViewModel>.Copy(costCalculationRetail_Material, costCalculationRetail_MaterialVM);

                    CategoryViewModel categoryVM = new CategoryViewModel()
                    {
                        Id = costCalculationRetail_Material.CategoryId
                    };
                    string[] names = costCalculationRetail_Material.CategoryName.Split(new[] { " - " }, StringSplitOptions.None);
                    categoryVM.Name = names[0];
                    try
                    {
                        categoryVM.SubCategory = names[1];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        categoryVM.SubCategory = null;
                    }

                    costCalculationRetail_MaterialVM.Category = categoryVM;
                    MaterialViewModel materialVM = new MaterialViewModel()
                    {
                        Id = costCalculationRetail_Material.MaterialId,
                        Name = costCalculationRetail_Material.MaterialName
                    };
                    costCalculationRetail_MaterialVM.Material = materialVM;

                    UOMViewModel uomQuantityVM = new UOMViewModel()
                    {
                        Id = costCalculationRetail_Material.UOMQuantityId,
                        Name = costCalculationRetail_Material.UOMQuantityName
                    };
                    costCalculationRetail_MaterialVM.UOMQuantity = uomQuantityVM;

                    UOMViewModel uomPriceVM = new UOMViewModel()
                    {
                        Id = costCalculationRetail_Material.UOMPriceId,
                        Name = costCalculationRetail_Material.UOMPriceName
                    };
                    costCalculationRetail_MaterialVM.UOMPrice = uomPriceVM;

                    viewModel.CostCalculationRetail_Materials.Add(costCalculationRetail_MaterialVM);
                }
            }

            return viewModel;
        }

        public CostCalculationRetail MapToModel(CostCalculationRetailViewModel viewModel)
        {
            CostCalculationRetail model = new CostCalculationRetail();

            PropertyCopier<CostCalculationRetailViewModel, CostCalculationRetail>.Copy(viewModel, model);

            model.StyleId = viewModel.Style._id;
            model.StyleName = viewModel.Style.name;
            model.SeasonId = viewModel.Season._id;
            model.SeasonCode = viewModel.Season.code;
            model.SeasonName = viewModel.Season.name;
            model.CounterId = viewModel.Counter._id;
            model.CounterName = viewModel.Counter.name;
            model.BuyerId = viewModel.Buyer.Id;
            model.BuyerName = viewModel.Buyer.Name;
            model.SizeRangeId = viewModel.SizeRange.Id;
            model.SizeRangeName = viewModel.SizeRange.Name;
            model.FabricAllowance = Percentage.ToFraction(viewModel.FabricAllowance);
            model.AccessoriesAllowance = Percentage.ToFraction(viewModel.AccessoriesAllowance);
            model.EfficiencyId = viewModel.Efficiency.Id;
            model.EfficiencyValue = viewModel.Efficiency.Value != null ? (double)viewModel.Efficiency.Value : 0;
            model.Risk = Percentage.ToFraction(viewModel.Risk);

            model.OLId = viewModel.OL.Id;
            model.OLRate = viewModel.OL.Value != null ? (double)viewModel.OL.Value : 0;
            model.OLCalculatedRate = viewModel.OL.CalculatedValue != null ? (double)viewModel.OL.CalculatedValue : 0;
            model.OTL1Id = viewModel.OTL1.Id;
            model.OTL1Rate = viewModel.OTL1.Value != null ? (double)viewModel.OTL1.Value : 0;
            model.OTL1CalculatedRate = viewModel.OTL1.CalculatedValue != null ? (double)viewModel.OTL1.CalculatedValue : 0;
            model.OTL2Id = viewModel.OTL2.Id;
            model.OTL2Rate = viewModel.OTL2.Value != null ? (double)viewModel.OTL2.Value : 0;
            model.OTL2CalculatedRate = viewModel.OTL2.CalculatedValue != null ? (double)viewModel.OTL2.CalculatedValue : 0;
            model.OTL3Id = viewModel.OTL3.Id;
            model.OTL3Rate = viewModel.OTL3.Value != null ? (double)viewModel.OTL3.Value : 0;
            model.OTL3CalculatedRate = viewModel.OTL3.CalculatedValue != null ? (double)viewModel.OTL3.CalculatedValue : 0;

            model.CostCalculationRetail_Materials = new List<CostCalculationRetail_Material>();

            foreach (CostCalculationRetail_MaterialViewModel costCalculationRetail_MaterialVM in viewModel.CostCalculationRetail_Materials)
            {
                CostCalculationRetail_Material costCalculationRetail_Material = new CostCalculationRetail_Material();
                PropertyCopier<CostCalculationRetail_MaterialViewModel, CostCalculationRetail_Material>.Copy(costCalculationRetail_MaterialVM, costCalculationRetail_Material);

                costCalculationRetail_Material.CategoryId = costCalculationRetail_MaterialVM.Category.Id;
                costCalculationRetail_Material.CategoryName = costCalculationRetail_MaterialVM.Category.SubCategory != null ? costCalculationRetail_MaterialVM.Category.Name + " - " + costCalculationRetail_MaterialVM.Category.SubCategory : costCalculationRetail_MaterialVM.Category.Name;
                costCalculationRetail_Material.MaterialId = costCalculationRetail_MaterialVM.Material.Id;
                costCalculationRetail_Material.MaterialName = costCalculationRetail_MaterialVM.Material.Name;
                costCalculationRetail_Material.UOMQuantityId = costCalculationRetail_MaterialVM.UOMQuantity.Id;
                costCalculationRetail_Material.UOMQuantityName = costCalculationRetail_MaterialVM.UOMQuantity.Name;
                costCalculationRetail_Material.UOMPriceId = costCalculationRetail_MaterialVM.UOMPrice.Id;
                costCalculationRetail_Material.UOMPriceName = costCalculationRetail_MaterialVM.UOMPrice.Name;

                model.CostCalculationRetail_Materials.Add(costCalculationRetail_Material);
            }

            return model;
        }
    }
}
