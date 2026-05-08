"""Allow running consumer with: python -m app.workers.consumer"""
import asyncio
from app.workers.consumer import main

if __name__ == "__main__":
    asyncio.run(main())
