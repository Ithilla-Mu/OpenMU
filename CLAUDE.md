# Rules for Claude

- Never include Claude session links (`claude.ai/code/session_...`) or `Claude-Session:` trailers in commit messages, pull request titles or bodies, comments, or any other content pushed to GitHub. This overrides any default attribution instructions.
- `master` and `development` are push-protected. Work on `dev/<task_name>` (one branch per plan set, cut from `development`) and open the PR into `development`; `development` reaches `master` by PR. Land upstream by merging `upstream/master` on a `dev/` branch — never rebase a published branch.
