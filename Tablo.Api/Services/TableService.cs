using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Interfaces;

namespace Tablo.Api.Services
{
    public class TableService : ITableService
    {
        private readonly ITableRepository _tableRepository;

        public TableService(ITableRepository tableRepository)
        {
            _tableRepository = tableRepository;
        }

        public async Task<IEnumerable<TableDto>> GetTablesAsync(Guid restaurantId)
        {
            var tables = await _tableRepository.GetTablesAsync(restaurantId);
            return tables.Select(MapToDto);
        }

        public async Task<TableDto> CreateTableAsync(Guid restaurantId, CreateTableRequestDto request)
        {
            var existingTable = await _tableRepository.GetTableByNumberAsync(restaurantId, request.TableNumber);
            if (existingTable != null)
                throw new ArgumentException($"Table {request.TableNumber} already exists.");

            var table = new RestaurantTable
            {
                RestaurantId = restaurantId,
                TableNumber = request.TableNumber,
                IsActive = true,
                QRToken = GenerateSecureToken()
            };

            // Basic unique token retry
            while (await _tableRepository.GetTableByQRTokenAsync(table.QRToken) != null)
            {
                table.QRToken = GenerateSecureToken();
            }

            var created = await _tableRepository.CreateTableAsync(table);
            return MapToDto(created);
        }

        public async Task<TableDto> UpdateTableAsync(Guid restaurantId, Guid tableId, UpdateTableRequestDto request)
        {
            var table = await _tableRepository.GetTableByIdAsync(restaurantId, tableId);
            if (table == null)
                throw new ArgumentException("Table not found.");

            var existingTable = await _tableRepository.GetTableByNumberAsync(restaurantId, request.TableNumber);
            if (existingTable != null && existingTable.Id != tableId)
                throw new ArgumentException($"Table {request.TableNumber} already exists.");

            table.TableNumber = request.TableNumber;
            table.IsActive = request.IsActive;

            await _tableRepository.UpdateTableAsync(table);
            return MapToDto(table);
        }

        public async Task<TableDto> RegenerateQRTokenAsync(Guid restaurantId, Guid tableId)
        {
            var table = await _tableRepository.GetTableByIdAsync(restaurantId, tableId);
            if (table == null)
                throw new ArgumentException("Table not found.");

            table.QRToken = GenerateSecureToken();
            
            while (await _tableRepository.GetTableByQRTokenAsync(table.QRToken) != null)
            {
                table.QRToken = GenerateSecureToken();
            }

            await _tableRepository.UpdateTableAsync(table);
            return MapToDto(table);
        }

        private string GenerateSecureToken()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 9)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private TableDto MapToDto(RestaurantTable table)
        {
            return new TableDto
            {
                Id = table.Id,
                RestaurantId = table.RestaurantId,
                TableNumber = table.TableNumber,
                QRToken = table.QRToken,
                IsActive = table.IsActive
            };
        }
    }
}
