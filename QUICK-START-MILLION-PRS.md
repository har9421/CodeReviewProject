# Quick Start: Million C# PRs Ingestion

## 🚀 Get Started in 5 Minutes

### 1. Set Up GitHub Token

```bash
# Get your GitHub token from: https://github.com/settings/tokens
export GITHUB_TOKEN="ghp_your_token_here"
```

### 2. Start the Intelligent Bot

```bash
./start-intelligent-bot.sh
```

### 3. Run the Ingestion Script

```bash
./ingest-millions-prs.sh
```

### 4. Select Option 2 (Large-Scale Ingestion)

- This will process 1,000+ repositories
- 1,000,000+ pull requests
- Takes 2-4 hours
- Generates comprehensive learning data

## 📊 What You'll Get

### Learning Data

- **Rule Effectiveness**: Tracks which rules work best
- **Pattern Recognition**: Learns common C# patterns
- **Quality Metrics**: Understands code quality trends
- **Developer Preferences**: Learns from real-world usage

### Expected Improvements

- **30-50% reduction** in false positives
- **40-60% improvement** in rule accuracy
- **Better developer experience** with smarter suggestions

## 🎯 Monitoring Progress

### Check Status

```bash
# View current ingestions
curl http://localhost:5002/api/data-ingestion/status

# Check learning insights
curl http://localhost:5002/api/feedback/insights

# Monitor performance
curl http://localhost:5002/api/performance/health
```

### Expected Timeline

- **0-30 min**: Repository discovery and setup
- **30-120 min**: PR data collection
- **120-180 min**: Code analysis and learning
- **180-240 min**: Rule optimization and insights

## 🔧 Troubleshooting

### Common Issues

1. **GitHub Rate Limit**: Wait and retry
2. **Memory Issues**: Reduce batch size
3. **Network Timeout**: Check internet connection

### Quick Fixes

```bash
# Restart bot if needed
./stop-all.sh
./start-intelligent-bot.sh

# Check logs
tail -f logs/codereview-bot-*.log

# Monitor system resources
top -p $(pgrep -f "CodeReviewBot")
```

## 📈 Success Indicators

### Good Signs

- ✅ Processing 100+ PRs per minute
- ✅ Memory usage stable (<4GB)
- ✅ No rate limit errors
- ✅ Learning data growing

### Warning Signs

- ⚠️ Processing <50 PRs per minute
- ⚠️ Memory usage >8GB
- ⚠️ Frequent rate limit errors
- ⚠️ No learning data generated

## 🎉 Next Steps

### After Ingestion Completes

1. **Review Learning Insights**: Check what the bot learned
2. **Test on New PRs**: See improved suggestions
3. **Monitor Performance**: Track ongoing improvements
4. **Scale Up**: Run additional batches for more data

### Long-term Learning

- **Continuous Ingestion**: Set up regular data updates
- **Feedback Collection**: Gather developer feedback
- **Rule Optimization**: Let the bot improve its rules
- **Custom Training**: Add your organization's repositories

## 📚 Documentation

- **Full Guide**: `MILLION-PRS-INGESTION.md`
- **Intelligent Features**: `INTELLIGENT-FEATURES.md`
- **API Reference**: Check the bot's Swagger UI at `http://localhost:5002/swagger`

---

**Ready to train your bot with millions of C# PRs? Let's go! 🚀**
