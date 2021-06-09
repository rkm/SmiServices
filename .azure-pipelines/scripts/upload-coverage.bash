#!/usr/bin/env bash

set -exo pipefail

#dotnet tool update --global coveralls.net

if [ ! -z "$SYSTEM_PULLREQUEST_PULLREQUESTNUMBER" ]
then
    commit_branch_arg="--commitBranch $SYSTEM_PULLREQUEST_SOURCEBRANCH"
    commit_id_arg="--commitId $SYSTEM_PULLREQUEST_SOURCECOMMITID"
    pull_request_arg="--pullRequest $SYSTEM_PULLREQUEST_PULLREQUESTNUMBER"
else
    commit_branch_arg="--commitBranch $BUILD_SOURCEBRANCHNAME"
    commit_id_arg="--commitId $BUILD_SOURCEVERSION"
    pull_request_arg=""
fi

set -u

echo csmacnz.Coveralls \
    --opencover \
    -i coverage/coverage.opencover.xml \
    --useRelativePaths \
    --commitAuthor "$BUILD_SOURCEVERSIONAUTHOR" \
    --commitEmail "$BUILD_REQUESTEDFOREMAIL" \
    --commitMessage "$BUILD_SOURCEVERSIONMESSAGE" \
    --jobId "$BUILD_BUILDID"
    $commit_id_arg \
    $commit_branch_arg \
    $pull_request_arg
