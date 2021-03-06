﻿using HeroesCup.Models;
using HeroesCup.Web.Models;
using HeroesCup.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Piranha;
using Piranha.AspNetCore.Services;
using System;
using System.Threading.Tasks;

namespace HeroesCup.Controllers
{
    public class CmsController : Controller
    {
        private readonly IApi api;
        private readonly IModelLoader loader;
        private readonly ILeaderboardService leaderboardService;
        private readonly IStatisticsService statisticsService;
        private readonly IMissionsService missionsService;
        private readonly IMetaDataProvider metaDataProvider;        

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="api">The current api</param>
        public CmsController(
            IApi api, 
            IModelLoader loader, 
            ILeaderboardService leaderboardService, 
            IStatisticsService statisticsService,
            IMissionsService missionsService,
            IMetaDataProvider metaDataProvider)
        {
            this.api = api;
            this.loader = loader;
            this.leaderboardService = leaderboardService;
            this.statisticsService = statisticsService;
            this.missionsService = missionsService;
            this.metaDataProvider = metaDataProvider;
        }

        /// <summary>
        /// Gets the blog archive with the given id.
        /// </summary>
        /// <param name="id">The unique page id</param>
        /// <param name="year">The optional year</param>
        /// <param name="month">The optional month</param>
        /// <param name="page">The optional page</param>
        /// <param name="category">The optional category</param>
        /// <param name="tag">The optional tag</param>
        /// <param name="draft">If a draft is requested</param>
        [Route("archive")]
        public async Task<IActionResult> Archive(Guid id, int? year = null, int? month = null, int? page = null,
            Guid? category = null, Guid? tag = null, bool draft = false)
        {
            var model = await this.loader.GetPageAsync<BlogArchive>(id, HttpContext.User, draft);
            model.Archive = await this.api.Archives.GetByIdAsync(id, page, category, tag, year, month);

            return View(model);
        }

        /// <summary>
        /// Gets the page with the given id.
        /// </summary>
        /// <param name="id">The unique page id</param>
        /// <param name="draft">If a draft is requested</param>
        [Route("page")]
        public async Task<IActionResult> Page(Guid id, bool draft = false)
        {
            var model = await this.loader.GetPageAsync<StandardPage>(id, HttpContext.User, draft);

            return View(model);
        }

        /// <summary>
        /// Gets the post with the given id.
        /// </summary>
        /// <param name="id">The unique post id</param>
        /// <param name="draft">If a draft is requested</param>
        [Route("post")]
        public async Task<IActionResult> Post(Guid id, bool draft = false)
        {
            var model = await this.loader.GetPostAsync<BlogPost>(id, HttpContext.User, draft);

            return View(model);
        }

        /// <summary>
        /// Gets the startpage with the given id.
        /// </summary>
        /// <param name="id">The unique page id</param>
        /// <param name="draft">If a draft is requested</param>
        [Route("/")]
        [Route("/home")]
        public async Task<IActionResult> Start(Guid id, String selectedSchoolYear = null, bool draft = false)
        {
            var model = await this.loader.GetPageAsync<StartPage>(id, HttpContext.User, draft);

            // Leaderboard
            model.SchoolYears = this.leaderboardService.GetSchoolYears();
            if (selectedSchoolYear != null)
            {
                model.SelectedSchoolYear = selectedSchoolYear;
            }
            else
            {
                model.SelectedSchoolYear = this.leaderboardService.GetLatestSchoolYear();
            }

            var clubsListModel = await this.leaderboardService.GetClubsBySchoolYearAsync(model.SelectedSchoolYear);
            model.Clubs = clubsListModel;

            // Statistics
            model.MissionsCount = this.statisticsService.GetAllMissionsCount();
            model.ClubsCount = this.statisticsService.GetAllClubsCount();
            model.HeroesCount = this.statisticsService.GetAllHeroesCount();
            model.HoursCount = this.statisticsService.GetAllHoursCount();

            model.Missions = await this.missionsService.GetPinnedMissionViewModels();
            model.SocialNetworksMetaData = this.metaDataProvider.getMetaData(HttpContext, model.Slug, model.Title);            

            return View(model);
        }

        /// <summary>
        /// Gets the landing page with the given id.
        /// </summary>
        /// <param name="id">The unique page id</param>
        /// <param name="draft">If a draft is requested</param>
        [Route("/landing")]
        public async Task<IActionResult> LandingPage(Guid id, bool draft = false)
        {
            var model = await this.loader.GetPageAsync<LandingPage>(id, HttpContext.User, draft);
            model.SocialNetworksMetaData = this.metaDataProvider.getMetaData(HttpContext, model.Slug, model.Title);

            return View(model);
        }

        /// <summary>
        /// Gets the about page with the given id.
        /// </summary>
        /// <param name="id">The unique page id</param>
        /// <param name="draft">If a draft is requested</param>
        [Route("/about")]
        public async Task<IActionResult> AboutPage(Guid id, bool draft = false)
        {
            var model = await this.loader.GetPageAsync<AboutPage>(id, HttpContext.User, draft);
            model.SocialNetworksMetaData = this.metaDataProvider.getMetaData(HttpContext, model.Slug, model.Title);

            return View(model);
        }
    }
}