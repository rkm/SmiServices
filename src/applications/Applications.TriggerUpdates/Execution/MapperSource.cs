﻿using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using JetBrains.Annotations;
using MapsDirectlyToDatabaseTable;
using Microservices.IdentifierMapper.Execution.Swappers;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Spontaneous;
using Rdmp.Core.DataLoad.Triggers;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories;
using Smi.Common.Helpers;
using Smi.Common.Messages.Updating;
using Smi.Common.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace TriggerUpdates.Execution
{
    public class MapperSource : ITriggerUpdatesSource
    {
        private ISwapIdentifiers _swapper;
        private TriggerUpdatesFromMapperOptions _cliOptions;
        private GlobalOptions _globalOptions;

        public MapperSource(GlobalOptions globalOptions, TriggerUpdatesFromMapperOptions cliOptions)
        {
            _cliOptions = cliOptions;
            _globalOptions = globalOptions;

            ImplementationManager.Load<MySqlImplementation>();
            ImplementationManager.Load<OracleImplementation>();
            ImplementationManager.Load<MicrosoftSQLImplementation>();
            ImplementationManager.Load<PostgreSqlImplementation>();

            try
            {
                var objectFactory = new MicroserviceObjectFactory();
                _swapper = objectFactory.CreateInstance<ISwapIdentifiers>(globalOptions.IdentifierMapperOptions.SwapperType, typeof(ISwapIdentifiers).Assembly);
            }
            catch (System.Exception ex)
            {
                throw new System.Exception($"Could not create IdentifierMapper Swapper with SwapperType:{globalOptions?.IdentifierMapperOptions?.SwapperType ?? "Null"}",ex);
            }

            if(_swapper == null)
                throw new ArgumentException("No SwapperType has been specified in GlobalOptions.IdentifierMapperOptions");
            
        }

        public IEnumerable<UpdateValuesMessage> GetUpdates()
        {
            var mappingTable = _globalOptions.IdentifierMapperOptions.Discover();
            var archiveTable = mappingTable.Database.ExpectTable(mappingTable.GetRuntimeName() + "_Archive");

            if(!archiveTable.Exists())
                throw new Exception($"No Archive table exists for mapping table {mappingTable.GetFullyQualifiedName()}");

            var swapCol = _globalOptions.IdentifierMapperOptions.SwapColumnName;
            var forCol = _globalOptions.IdentifierMapperOptions.ReplacementColumnName;

            var archiveFetchSql = GetArchiveFetchSql(archiveTable,swapCol,forCol);

            using(var con = mappingTable.Database.Server.GetConnection())
            {
                con.Open();

                var dateOfLastUpdate = _cliOptions.DateOfLastUpdate;

                
                //find all records in the table that are new
                var cmd = mappingTable.GetCommand($"SELECT {swapCol}, {forCol} FROM {mappingTable.GetFullyQualifiedName()} WHERE {SpecialFieldNames.ValidFrom} >= @dateOfLastUpdate",con);
                mappingTable.Database.Server.AddParameterWithValueToCommand("@dateOfLastUpdate",cmd,dateOfLastUpdate);
                

                using(var r = cmd.ExecuteReader())
                {
                    while(r.Read())
                    {
                        var currentSwapColValue = r[swapCol];
                        var currentForColValue = r[forCol];

                        //if there is a corresponding record in the archive table
                        using(var con2 = archiveTable.Database.Server.GetConnection())
                        {
                            con2.Open();
                            var cmd2 = archiveTable.GetCommand(archiveFetchSql,con2);

                            archiveTable.Database.Server.AddParameterWithValueToCommand("@currentSwapColValue",cmd2,currentSwapColValue);

                            var oldForColValue = cmd2.ExecuteScalar();

                            //if there is no entry in the archive for this old one it must be brand new i.e. not an update
                            if(oldForColValue == null)
                                continue;

                            //there is an entry in the archive so we need to issue a database update to update the live tables so the old archive
                            // table swap value (e.g. ECHI) is updated to the new one in the live table
                            yield return new UpdateValuesMessage()
                            {
                                WhereFields = new []{ forCol},
                                HaveValues = new []{ oldForColValue?.ToString()},

                                WriteIntoFields = new []{ forCol},
                                Values = new []{ currentForColValue?.ToString()}
                            };

                        }

                        // TODO we should also look at guid mappings that are filled in now
                        if(!(_swapper is TableLookupWithGuidFallbackSwapper guidSwapper))
                            throw new NotSupportedException("Currently only TableLookupWithGuidFallbackSwapper is supported by this class");
                    }
                }
            }
        }


        /// <summary>
        /// Returns a query for fetching the latest entry in the archive that matches a given private identifier (query contains parameter @currentSwapColValue)
        /// </summary>
        /// <param name="archiveTable"></param>
        /// <param name="swapCol">The private identifier column name e.g. CHI</param>
        /// <param name="forCol">The public release identifier column name e.g. ECHI</param>
        /// <returns>SQL for fetching the latest release identifier value (e.g. ECHI value) from the archive</returns>
        private string GetArchiveFetchSql(DiscoveredTable archiveTable,string swapCol,string forCol)
        {
            // Work out how to get the latest entry in the _Archive table that corresponds to a given private identifier (e.g. CHI)
            var syntax = archiveTable.Database.Server.GetQuerySyntaxHelper();

            var topX = syntax.HowDoWeAchieveTopX(1);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SELECT ");

            if(topX.Location == QueryComponent.SELECT)
                sb.AppendLine(topX.SQL);

            sb.AppendLine(forCol);
            sb.AppendLine("FROM " + archiveTable.GetFullyQualifiedName());
            sb.AppendLine("WHERE");
            sb.AppendLine($"{swapCol} = @currentSwapColValue");

            if(topX.Location == QueryComponent.WHERE)
            {
                sb.AppendLine("AND");
                sb.AppendLine(topX.SQL);
            }

            sb.AppendLine("ORDER BY");
            sb.AppendLine(SpecialFieldNames.ValidFrom + " desc");

            if(topX.Location == QueryComponent.Postfix)
                sb.AppendLine(topX.SQL);

            return sb.ToString();
        }
    }
}