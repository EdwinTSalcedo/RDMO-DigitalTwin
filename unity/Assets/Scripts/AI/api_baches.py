"""Compatibility launcher for the RDMO Python inference server.

Older project notes referenced this file directly. The maintained server now
lives in api_model_pt.py and loads models/model_finetuned.pt by default.
"""

import uvicorn

from api_model_pt import app


if __name__ == "__main__":
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=5000
    )
