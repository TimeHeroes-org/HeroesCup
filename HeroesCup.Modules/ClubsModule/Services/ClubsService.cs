﻿using ClubsModule.Common;
using ClubsModule.Models;
using ClubsModule.Services.Contracts;
using HeroesCup.Data;
using HeroesCup.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClubsModule.Services
{
    public class ClubsService : IClubsService
    {
        private readonly HeroesCupDbContext dbContext;
        private readonly IImagesService imagesService;
        private readonly IHeroesService heroesService;
        private readonly IConfiguration configuration;

        private string dateTimeFormat;

        public ClubsService(HeroesCupDbContext dbContext, IImagesService imagesService, IHeroesService heroesService, IConfiguration configuration)
        {
            this.dbContext = dbContext;
            this.imagesService = imagesService;
            this.heroesService = heroesService;
            this.configuration = configuration;
            this.dateTimeFormat = this.configuration["DateТimeFormat"];
        }

        public async Task<ClubEditModel> CreateClubEditModelAsync(Guid? ownerId)
        {
            var missions = new List<Mission>();
            var heroes = new List<Hero>();
            if (ownerId.HasValue)
            {
                missions = await this.dbContext.Missions.Where(m => m.OwnerId == ownerId.Value).ToListAsync();
                heroes = await this.dbContext.Heroes.Where(h => h.Club.OwnerId == ownerId.Value).ToListAsync();
            }
            else
            {
                missions = await this.dbContext.Missions.ToListAsync();
                heroes = await this.dbContext.Heroes.ToListAsync();
            }

            var model = new ClubEditModel()
            {
                Club = new Club(),
                Missions = missions != null ? missions : new List<Mission>(),
                Heroes = heroes != null ? heroes : new List<Hero>()
            };

            model.Club.OwnerId = ownerId.HasValue ? ownerId.Value : Guid.Empty;
            return model;
        }

        public async Task<ClubEditModel> GetClubEditModelByIdAsync(Guid id, Guid? ownerId)
        {
            var club = await this.dbContext.Clubs
                    .Include(c => c.ClubImages)
                    .FirstOrDefaultAsync(c => c.Id == id);

            if (club == null)
            {
                return null;
            }

            if (ownerId.HasValue && club.OwnerId != ownerId)
            {
                return null;
            }

            var missions = club.Missions;

            var model = await CreateClubEditModelAsync(ownerId);
            model.Club = club;
            var coordinators = await this.GetClubCoordinatorsAsync(club.Id);
            if (coordinators != null)
            {
                model.Coordinators = coordinators;
                model.CoordinatorsIds = coordinators.Select(c => c.Id).ToArray();
            }

            if (club.ClubImages != null && club.ClubImages.Count > 0)
            {
                var clubImage = await this.imagesService.GetClubImage(club.Id);
                model.ClubImageId = Guid.Empty != clubImage.ImageId ? clubImage.ImageId.ToString() : null;
            }

            model.Missions = club.Missions;
            model.Heroes = club.Heroes;

            return model;
        }

        public async Task<ClubListModel> GetClubListModelAsync(Guid? ownerId)
        {
            var clubs = new List<Club>();
            clubs = await this.dbContext.Clubs
                    .Include(c => c.Heroes)
                    .OrderByDescending(c => c.UpdatedOn)
                    .ToListAsync();

            if (ownerId.HasValue)
            {
                clubs = clubs.Where(c => c.OwnerId == ownerId.Value).ToList();
            }

            var model = new ClubListModel()
            {
                Clubs = clubs.Select(c => new ClubListItem()
                                {
                                    Id = c.Id,
                                    Name = c.Name,
                                    OrganizationType = c.OrganizationType,
                                    OrganizationName = c.OrganizationName,
                                    OrganizationNumber = c.OrganizationNumber,
                                    HeroesCount = c.Heroes != null ? c.Heroes.Count() : 0,
                                    LastUpdateOn = c.UpdatedOn.ToUniversalDateTime().ToLocalTime().ToString(this.dateTimeFormat)
        })

            };

            return model;
        }

        public async Task<Guid> SaveClubEditModelAsync(ClubEditModel model)
        {
            var club = await this.dbContext.Clubs
                .Include(c => c.ClubImages)
                .Include(c => c.Heroes)
                .Include(c => c.Missions)
                .FirstOrDefaultAsync(h => h.Id == model.Club.Id && h.OwnerId == model.Club.OwnerId);

            if (club == null)
            {
                club = new Club();
                club.Id = model.Club.Id != Guid.Empty ? model.Club.Id : Guid.NewGuid();
                club.OwnerId = model.Club.OwnerId;
                club.CreatedOn = DateTime.Now.ToUnixMilliseconds();
                this.dbContext.Clubs.Add(club);
            }

            club.Name = model.Club.Name.TrimInput();
            club.Location = model.Club.Location;
            club.OrganizationType = model.Club.OrganizationType;
            club.OrganizationName = model.Club.OrganizationName.TrimInput();
            club.OrganizationNumber = model.Club.OrganizationNumber.TrimInput();
            club.Description = model.Club.Description;


            // set club's heroes
            if (model.HeroesIds != null && model.HeroesIds.Any())
            {
                club.Heroes = new List<Hero>();
                foreach (var heroId in model.HeroesIds)
                {
                    var hero = this.dbContext.Heroes.FirstOrDefault(h => h.Id == heroId);
                    club.Heroes.Add(hero);
                }
            }

            // set club coordinators
            if (model.CoordinatorsIds != null && model.CoordinatorsIds.All(c => c != Guid.Empty))
            {
                await this.heroesService.SaveCoordinatorsAsync(model.CoordinatorsIds, club, false);            
            }

            // set club's missions
            if (model.MissionsIds != null && model.MissionsIds.Any())
            {
                club.Missions = new List<Mission>();
                foreach (var missionId in model.MissionsIds)
                {
                    var mission = this.dbContext.Missions.FirstOrDefault(h => h.Id == missionId);
                    club.Missions.Add(mission);
                }
            }

            // set club logo
            if (model.UploadedLogo != null)
            {
                var image = this.imagesService.MapFormFileToImage(model.UploadedLogo);
                await this.imagesService.CreateClubImageAsync(image, club);
            }

            club.UpdatedOn = DateTime.Now.ToUnixMilliseconds();

            await dbContext.SaveChangesAsync();
            return club.Id;
        }

        public async Task<IEnumerable<Hero>> GetClubCoordinatorsAsync(Guid clubId)
        {
            IEnumerable<Hero> coordinators = null;
            var club = await this.dbContext.Clubs.FirstOrDefaultAsync(c => c.Id == clubId);
            if (club == null)
            {
                return null;
            }

            if (club.Heroes != null && club.Heroes.Count > 0)
            {
                coordinators = club.Heroes.Where(c => c.IsCoordinator);
            }

            return coordinators;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var club = this.dbContext.Clubs.FirstOrDefault(c => c.Id == id);
            if (club == null)
            {
                return false;
            }

            this.dbContext.Clubs.Remove(club);
            await this.dbContext.SaveChangesAsync();
            return true;
        }

        public IEnumerable<Club> GetAllClubs()
        {
            return this.dbContext.Clubs
                .Include(c => c.Heroes);
        }
    }
}