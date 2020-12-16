using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrackerLibrary.Models;

namespace TrackerLibrary
{
    public static class TournamentLogic
    {
        //Order our list randomly of teams- random picking of teams
        //Check if it is big enough - if not, add in byes - 2*2*2*2 = 16 teams = 2^4
        //Create out first rounds of matchups
        //Create every round after that - for 16 teams - 8 matchups then 4 matchups then 2 matchups then 1 matchup

        public static void CreateRounds(TournamentModel model)
        {
            List<TeamModel> randomizedTeams = RandomizeTeamOrder(model.EnteredTeams);
            int rounds = FindNumberOfRounds(randomizedTeams.Count);
            int byes = NumberOfByes(rounds, randomizedTeams.Count);

            model.Rounds.Add(CreateFirstRound(byes, randomizedTeams));
            CreateOtherRounds(model, rounds);

            
        }

        public static void updateTournamentResults(TournamentModel model)
        {
            int startingRound = model.CheckCurrentRound();
            
            List<MatchupModel> toScore = new List<MatchupModel>();

            foreach(List<MatchupModel> round in model.Rounds)
            {
                foreach(MatchupModel rm in round)
                {
                    if(rm.Winner == null && (rm.Entries.Any(x => x.Score != 0)|| rm.Entries.Count == 1))
                    {
                        toScore.Add(rm);
                    }
                }
            }

            MarkWinnerInMatchups(toScore);

            AdvanceWinners(toScore, model);

            toScore.ForEach(x => GlobalConfig.Connection.UpdateMatchup(x));

            int endingRound = model.CheckCurrentRound();

            if(endingRound > startingRound)
            {
                //Alert users
                //EmailLogic.SendEmail()
            }
        }

        private static void AlertUsersToNewRound(this TournamentModel model)
        {
            int currentRoundNumber = model.CheckCurrentRound();
            List<MatchupModel> currentRound = model.Rounds.Where(x => x.First().MatchupRound == currentRoundNumber).First();

            foreach (MatchupModel matchup in currentRound)
            {
                foreach (MatchupEntryModel me in matchup.Entries)
                {
                    foreach (PersonModel p in me.TeamCompeting.TeamMembers)
                    {
                        AlertPersonToNewRound(p, me.TeamCompeting.TeamName, matchup.Entries.Where(x => x.TeamCompeting != me.TeamCompeting).FirstOrDefault());
                    }
                }
            }
        }

        private static void AlertPersonToNewRound(PersonModel p, string teamName, MatchupEntryModel competitor)
        {
            string from = "";
            List<string> to = new List<string>();
            string subject = "";
            string body = "";
            //stringbuilder to concatenate strings
            StringBuilder sb = new StringBuilder();

            if(competitor != null)
            {
                subject = $"You have a new matchup with { competitor.TeamCompeting.TeamName }";
            }
            else
            {
                subject = "You have a bye week this round.";
            }

            sb.AppendLine("<h1>You have a new matchup</h1>");
            sb.AppendLine("<strong>Competitor: </strong>");
            sb.Append(competitor.TeamCompeting.TeamName);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Have a great time!");

            EmailLogic.SendEmail(from, to, subject, body);
        }

        private static int CheckCurrentRound(this TournamentModel model)
        {
            int output = 1;
            foreach(List<MatchupModel> round in model.Rounds)
            {
                if (round.All(x => x.Winner != null))
                {
                    output += 1;
                }
            }
            return output;
        }

        private static void AdvanceWinners(List<MatchupModel> models, TournamentModel tournament)
        {
            foreach(MatchupModel m in models)
            {
                foreach (List<MatchupModel> round in tournament.Rounds)
                {
                    foreach (MatchupModel rm in round)
                    {
                        foreach (MatchupEntryModel me in rm.Entries)
                        {
                            if (me.ParentMatchup != null)
                            {
                                if (me.ParentMatchup.Id == m.Id)
                                {
                                    me.TeamCompeting = m.Winner;
                                    GlobalConfig.Connection.UpdateMatchup(rm);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void MarkWinnerInMatchups(List<MatchupModel> models)
        {
            //if (teamOneScore > teamTwoScore)
            //{
            //    //team one wins
            //    m.Winner = m.Entries[0].TeamCompeting;
            //}
            //else if (teamTwoScore > teamOneScore)
            //{
            //    m.Winner = m.Entries[1].TeamCompeting;
            //}
            //else
            //{
            //    MessageBox.Show("I do not deal with Tie games.");
            //}

            //greater or lesser
            string greaterWins = ConfigurationManager.AppSettings["greaterWins"];

            foreach(MatchupModel m in models)
            {
                // Check for bye week entry
                if(m.Entries.Count == 1)
                {
                    m.Winner = m.Entries[0].TeamCompeting;
                    continue;
                }

                //0 means false, or low score wins
                if (greaterWins == "0")
                {
                    if(m.Entries[0].Score < m.Entries[1].Score)
                    {
                        m.Winner = m.Entries[0].TeamCompeting;
                    }
                    else if (m.Entries[1].Score < m.Entries[0].Score)
                    {
                        m.Winner = m.Entries[1].TeamCompeting;
                    }
                    else
                    {
                        throw new Exception("We do not allow ties in this application.");
                    }
                }
                else
                {
                    //1 means true, or high score wins
                    if (m.Entries[0].Score > m.Entries[1].Score)
                    {
                        m.Winner = m.Entries[0].TeamCompeting;
                    }
                    else if (m.Entries[1].Score > m.Entries[0].Score)
                    {
                        m.Winner = m.Entries[1].TeamCompeting;
                    }
                    else
                    {
                        throw new Exception("We do not allow ties in this application.");
                    }
                }
            }
        }

        private static void CreateOtherRounds(TournamentModel model, int rounds)
        {
            int round = 2;
            List<MatchupModel> previousRound = model.Rounds[0];
            List<MatchupModel> currRound = new List<MatchupModel>();
            MatchupModel currMatchup = new MatchupModel();

            while(round <= rounds)
            {
                foreach(MatchupModel match in previousRound)
                {
                    currMatchup.Entries.Add(new MatchupEntryModel { ParentMatchup = match });
                    if(currMatchup.Entries.Count > 1)
                    {
                        currMatchup.MatchupRound = round;
                        currRound.Add(currMatchup);
                        currMatchup = new MatchupModel();
                    }
                }

                model.Rounds.Add(currRound);
                //clean up
                previousRound = currRound;
                currRound = new List<MatchupModel>();
                round += 1;
            }
        }

        private static List<MatchupModel> CreateFirstRound(int byes, List<TeamModel> teams)
        {
            List<MatchupModel> output = new List<MatchupModel>();
            MatchupModel curr = new MatchupModel();

            foreach(TeamModel team in teams)
            {
                curr.Entries.Add(new MatchupEntryModel { TeamCompeting = team });

                if(byes > 0 || curr.Entries.Count > 1)
                {
                    curr.MatchupRound = 1;
                    output.Add(curr);
                    curr = new MatchupModel();

                    if(byes > 0)
                    {
                        byes -= 1;
                    }
                }
            }
            return output;
        }
       
        private static int NumberOfByes(int rounds, int numberOfTeams)
        {
            int output = 0;

            //Find out how many teams in a round is required
            int totalTeams = 1;

            for(int i = 1; i <= rounds; i++)
            {
                totalTeams *= 2;
            }

            //no of byes = output 
            //total number of teams - actual number of teams
            output = totalTeams - numberOfTeams;
            return output;
        }

        public static int FindNumberOfRounds(int teamCount)
        {
            int output = 0;
            int val = 2;

            //trying to figure out how many rounds we need to go
            while (val < teamCount)
            {
                output += 1;
                val *= 2;
            }

            return output;
        }

        private static List<TeamModel> RandomizeTeamOrder(List<TeamModel> teams)
        {
            //cards.OrderBy(a => Guid.NewGuid()).ToList();
            return teams.OrderBy(x => Guid.NewGuid()).ToList();
        }
    }
}
